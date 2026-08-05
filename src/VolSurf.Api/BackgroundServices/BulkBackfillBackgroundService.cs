using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using VolSurf.Core.BlackScholes;
using VolSurf.Core.Services;
using VolSurf.Data.Entities;
using VolSurf.Data.Repositories;

namespace VolSurf.Api.BackgroundServices;

/// <summary>
/// 批量回算请求：单标的，全交易日遍历。
/// </summary>
public record BulkBackfillRequest(string Underlying);

/// <summary>
/// 批量回算 BackgroundService：
///   对单个标的，按交易日遍历 options_daily，逐日计算 IV/Greeks 并 upsert 到 options_iv_greeks。
///
/// 历史回算的特殊问题：underlying_daily 没有每日记录。
///   这里采用 put-call parity 估算当日标的价格：
///     对每个交易日，从同 strike 的 Call / Put 配对反推 S：
///       S ≈ C - P + K * e^(-r * T_mean)
///     然后取所有同到期月的 (C - P + K*e^(-rT)) 的中位数作为当日 underlying price。
///   这样无需 Tushare 拉历史 ETF 日线即可推进回算。
///
/// 输出：每个交易日打印一次进度（已处理 / 总交易日数 / 总合约数 / 计算成功 / 跳过 / 异常）。
/// </summary>
public class BulkBackfillBackgroundService(
    Channel<BulkBackfillRequest> channel,
    IServiceProvider serviceProvider,
    ILogger<BulkBackfillBackgroundService> logger) : BackgroundService
{
    private const double RiskFreeRate = 0.02;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("BulkBackfillBackgroundService started");

        try
        {
            await foreach (var req in channel.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await ProcessAsync(req, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,
                        "Bulk backfill failed for underlying={Underlying}",
                        req.Underlying);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 关闭信号
        }

        logger.LogInformation("BulkBackfillBackgroundService stopped");
    }

    private async Task ProcessAsync(BulkBackfillRequest req, CancellationToken ct)
    {
        using var scope = serviceProvider.CreateScope();
        var calcService = scope.ServiceProvider.GetRequiredService<IvCalculationService>();
        var optRepo = scope.ServiceProvider.GetRequiredService<IOptionRepository>();
        var ulRepo = scope.ServiceProvider.GetRequiredService<IUnderlyingRepository>();
        var db = scope.ServiceProvider.GetRequiredService<VolSurf.Data.VolSurfDbContext>();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var underlying = req.Underlying;

        // 1. 取全部交易日
        var tradeDates = await db.OptionDaily
            .Where(d => d.Underlying == underlying)
            .Select(d => d.TradeDate)
            .Distinct()
            .OrderBy(d => d)
            .ToListAsync(ct);

        if (tradeDates.Count == 0)
        {
            logger.LogWarning(
                "No option_daily data for {Underlying}, skip", underlying);
            return;
        }

        logger.LogInformation(
            "BulkBackfill start: underlying={Underlying}, tradeDates={Dates}, totalDailyRows={Rows}",
            underlying, tradeDates.Count,
            await db.OptionDaily.CountAsync(d => d.Underlying == underlying, ct));

        // 2. 预拉合约主表（一次性，避免每交易日重复读）
        var allContracts = await db.OptionContracts
            .Where(c => c.Underlying == underlying)
            .ToListAsync(ct);
        var contractMap = allContracts.ToDictionary(c => c.TsCode);

        int dayIdx = 0;
        int totalProcessed = 0, totalCalculated = 0, totalSkipped = 0, totalAnomaly = 0;

        foreach (var tradeDate in tradeDates)
        {
            ct.ThrowIfCancellationRequested();
            dayIdx++;
            var td = tradeDate.Date;

            // 2a. 取当日 underlying daily（如果有）
            double underlyingPrice = 0;
            var ulDaily = await ulRepo.GetUnderlyingDailyAsync(underlying, td);
            if (ulDaily != null && ulDaily.Close > 0)
            {
                underlyingPrice = (double)ulDaily.Close;
            }
            else
            {
                // 2b. 用 put-call parity 从当日期权反推 underlying price
                underlyingPrice = await EstimateUnderlyingFromParityAsync(
                    db, underlying, td, contractMap);
                if (underlyingPrice > 0)
                {
                    // 写回 underlying_daily 便于以后直接查
                    try
                    {
                        await ulRepo.BulkUpsertUnderlyingDailyAsync(new[]
                        {
                            new UnderlyingDaily
                            {
                                TsCode = underlying,
                                TradeDate = td,
                                Close = (decimal)underlyingPrice
                            }
                        });
                    }
                    catch
                    {
                        // 非关键失败，继续
                    }
                }
            }

            if (underlyingPrice <= 0)
            {
                logger.LogWarning(
                    "[{Day}/{Total}] {Date} underlying price unavailable, skip",
                    dayIdx, tradeDates.Count, td.ToString("yyyy-MM-dd"));
                continue;
            }

            // 3. 当日 dailys
            var dailyData = await db.OptionDaily
                .Where(d => d.Underlying == underlying && d.TradeDate == td)
                .ToListAsync(ct);

            if (dailyData.Count == 0) continue;

            // 4. 逐合约计算
            var results = new List<IvGreeks>();
            int dayCalc = 0, daySkip = 0, dayAnom = 0;

            foreach (var daily in dailyData)
            {
                if (!contractMap.TryGetValue(daily.TsCode, out var contract))
                {
                    // 合约主表缺失（如历史已退市合约）：写一行 NULL 占位，确保
                    // options_iv_greeks 行数与 options_daily 对齐；不计入 calc/skip 计数。
                    results.Add(new IvGreeks
                    {
                        TsCode = daily.TsCode,
                        TradeDate = td,
                        Underlying = underlying,
                        Iv = null,
                        Delta = null,
                        Gamma = null,
                        Theta = null,
                        Vega = null,
                        Rho = null,
                        IvConfidence = false,
                        IvAnomaly = false
                    });
                    continue;
                }

                var iv = calcService.Calculate(daily, contract, underlyingPrice);
                if (iv.Iv == null)
                {
                    daySkip++;
                    // 也写一行，标记 IV=null / iv_confidence=false
                    results.Add(new IvGreeks
                    {
                        TsCode = daily.TsCode,
                        TradeDate = td,
                        Underlying = underlying,
                        Iv = null,
                        Delta = null,
                        Gamma = null,
                        Theta = null,
                        Vega = null,
                        Rho = null,
                        IvConfidence = false,
                        IvAnomaly = false
                    });
                    continue;
                }

                if (iv.Anomaly) dayAnom++;

                results.Add(new IvGreeks
                {
                    TsCode = daily.TsCode,
                    TradeDate = td,
                    Underlying = underlying,
                    Iv = iv.Iv.HasValue ? (decimal?)iv.Iv.Value : null,
                    Delta = iv.Delta.HasValue ? (decimal?)iv.Delta.Value : null,
                    Gamma = iv.Gamma.HasValue ? (decimal?)iv.Gamma.Value : null,
                    Theta = iv.Theta.HasValue ? (decimal?)iv.Theta.Value : null,
                    Vega = iv.Vega.HasValue ? (decimal?)iv.Vega.Value : null,
                    Rho = iv.Rho.HasValue ? (decimal?)iv.Rho.Value : null,
                    IvConfidence = iv.Confidence,
                    IvAnomaly = iv.Anomaly
                });
                dayCalc++;
            }

            // 5. 批量 upsert
            if (results.Count > 0)
            {
                await optRepo.BulkUpsertIvGreeksAsync(results);
            }

            totalProcessed += dailyData.Count;
            totalCalculated += dayCalc;
            totalSkipped += daySkip;
            totalAnomaly += dayAnom;

            // 每 1000 条 IV 算完后 flush 一次 + 给 EF Core 一个 SaveContext 机会
            if (dayIdx % 100 == 0)
            {
                logger.LogInformation(
                    "[{Day}/{Total}] processed {Date} | S={S:F4} contracts={Ct} calc={Calc} skip={Skip} anom={Anom}",
                    dayIdx, tradeDates.Count, td.ToString("yyyy-MM-dd"),
                    underlyingPrice, dailyData.Count, dayCalc, daySkip, dayAnom);
            }
        }

        sw.Stop();
        logger.LogInformation(
            "BulkBackfill done: underlying={Underlying} days={Dates} rows={Rows} calc={Calc} skip={Skip} anom={Anom} elapsed={Ms}ms",
            underlying, tradeDates.Count, totalProcessed,
            totalCalculated, totalSkipped, totalAnomaly, sw.ElapsedMilliseconds);
    }

    /// <summary>
    /// 用 put-call parity 反推当日 underlying price：
    ///   S ≈ C - P + K*e^(-rT)
    /// 对所有 (call, put) 同 strike + 接近 maturity 的配对求中位数。
    /// </summary>
    private async Task<double> EstimateUnderlyingFromParityAsync(
        VolSurf.Data.VolSurfDbContext db,
        string underlying,
        DateTime tradeDate,
        Dictionary<string, OptionContract> contractMap)
    {
        // 选最近的到期月（maturity > trade_date）
        var expiries = contractMap.Values
            .Where(c => c.MaturityDate.Date > tradeDate.Date)
            .Select(c => c.MaturityDate.Date)
            .Distinct()
            .OrderBy(d => d)
            .Take(2) // 用最近 1~2 个到期月
            .ToList();

        if (expiries.Count == 0) return 0;

        // 拉这些到期月当日的 call/put
        var dailyRows = await db.OptionDaily
            .Where(d => d.Underlying == underlying
                        && d.TradeDate == tradeDate.Date
                        && d.Settle != null && d.Settle > 0)
            .ToListAsync();

        var sEstimates = new List<double>();
        foreach (var exp in expiries)
        {
            // 配对：同 strike 的 call + put
            var rowsForExpiry = dailyRows
                .Where(r => contractMap.TryGetValue(r.TsCode, out var c) && c.MaturityDate.Date == exp)
                .ToList();

            // group by strike
            var byStrike = rowsForExpiry
                .Where(r => contractMap[r.TsCode].CallPut == "C")
                .Select(r => new
                {
                    Strike = (double)contractMap[r.TsCode].ExercisePrice,
                    R = r
                })
                .GroupBy(x => x.Strike)
                .ToDictionary(g => g.Key, g => g.First().R);

            foreach (var kv in byStrike)
            {
                double K = kv.Key;
                var callRow = kv.Value;
                var putRow = rowsForExpiry.FirstOrDefault(r =>
                    r.TsCode != callRow.TsCode
                    && contractMap.TryGetValue(r.TsCode, out var cc)
                    && cc.ExercisePrice == (decimal)K
                    && cc.CallPut == "P");

                if (putRow == null) continue;
                double T = (exp - tradeDate.Date).TotalDays / 365.0;
                if (T <= 0) continue;

                double C = (double)callRow.Settle!.Value;
                double P = (double)putRow.Settle!.Value;
                double sEst = C - P + K * Math.Exp(-RiskFreeRate * T);
                if (sEst > 0)
                    sEstimates.Add(sEst);
            }
        }

        if (sEstimates.Count == 0) return 0;

        // 取中位数（最稳健）
        sEstimates.Sort();
        return sEstimates[sEstimates.Count / 2];
    }
}
