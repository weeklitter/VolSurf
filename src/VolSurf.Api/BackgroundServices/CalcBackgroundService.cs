using System.Threading.Channels;
using VolSurf.Core.Services;
using VolSurf.Data.Entities;
using VolSurf.Data.Repositories;

namespace VolSurf.Api.BackgroundServices;

/// <summary>
/// 计算任务：单标的 + 单交易日的 IV/Greeks 批处理。
/// </summary>
public record CalcTask(DateTime TradeDate, string Underlying);

/// <summary>
/// 后台计算服务：从 <see cref="Channel{CalcTask}"/> 消费计算任务，
/// 逐日逐标的对当日全量合约执行 IV 数值求解 + Greeks 计算，
/// 并把结果 upsert 到 <c>options_iv_greeks</c>，最后刷新 <c>iv_percentile_cache</c>。
///
/// 寄宿在 VolSurf.Api 进程内（替代独立的 VolSurf.Worker 项目）。
/// 异常隔离：单个任务失败不会中断 Channel 消费循环。
/// </summary>
public class CalcBackgroundService(
    Channel<CalcTask> channel,
    IServiceProvider serviceProvider,
    ILogger<CalcBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("CalcBackgroundService started");

        try
        {
            await foreach (var task in channel.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await ProcessTaskAsync(task, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,
                        "Calc task failed for underlying={Underlying} date={TradeDate}",
                        task.Underlying, task.TradeDate.ToString("yyyy-MM-dd"));
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 关闭信号，正常退出
        }

        logger.LogInformation("CalcBackgroundService stopped");
    }

    private async Task ProcessTaskAsync(CalcTask task, CancellationToken ct)
    {
        logger.LogInformation(
            "Start IV/Greeks calculation: underlying={Underlying}, tradeDate={TradeDate}",
            task.Underlying, task.TradeDate.ToString("yyyy-MM-dd"));

        var sw = System.Diagnostics.Stopwatch.StartNew();

        using var scope = serviceProvider.CreateScope();
        var calcService = scope.ServiceProvider.GetRequiredService<IvCalculationService>();
        var validationService = scope.ServiceProvider.GetRequiredService<DataValidationService>();
        var percentileService = scope.ServiceProvider.GetRequiredService<IvPercentileService>();
        var optRepo = scope.ServiceProvider.GetRequiredService<IOptionRepository>();
        var ulRepo = scope.ServiceProvider.GetRequiredService<IUnderlyingRepository>();

        var tradeDate = task.TradeDate.Date;

        // 1. 读取当日数据
        var dailyData = await optRepo.GetOptionDailyAsync(tradeDate, task.Underlying);
        var contracts = await optRepo.GetActiveContractsAsync(task.Underlying, tradeDate);
        var ulDaily = await ulRepo.GetLatestUnderlyingDailyAsync(task.Underlying);
        double ulPrice = ulDaily != null ? (double)ulDaily.Close : 0;

        if (dailyData.Count == 0)
        {
            logger.LogWarning(
                "No option daily data for {Underlying} on {TradeDate}, skip",
                task.Underlying, tradeDate.ToString("yyyy-MM-dd"));
            return;
        }

        var contractMap = contracts.ToDictionary(c => c.TsCode);

        // 2. 逐合约计算
        var results = new List<IvGreeks>();
        int calculated = 0, skipped = 0, anomalies = 0;

        foreach (var daily in dailyData)
        {
            if (!contractMap.TryGetValue(daily.TsCode, out var contract))
                continue;

            var ivResult = calcService.Calculate(daily, contract, ulPrice);

            if (ivResult.Iv == null)
            {
                skipped++;
                continue;
            }

            if (ivResult.Anomaly)
                anomalies++;

            results.Add(new IvGreeks
            {
                TsCode = daily.TsCode,
                TradeDate = tradeDate,
                Underlying = task.Underlying,
                Iv = ivResult.Iv.HasValue ? (decimal?)ivResult.Iv.Value : null,
                Delta = ivResult.Delta.HasValue ? (decimal?)ivResult.Delta.Value : null,
                Gamma = ivResult.Gamma.HasValue ? (decimal?)ivResult.Gamma.Value : null,
                Theta = ivResult.Theta.HasValue ? (decimal?)ivResult.Theta.Value : null,
                Vega = ivResult.Vega.HasValue ? (decimal?)ivResult.Vega.Value : null,
                Rho = ivResult.Rho.HasValue ? (decimal?)ivResult.Rho.Value : null,
                IvConfidence = ivResult.Confidence,
                IvAnomaly = ivResult.Anomaly
            });
            calculated++;
        }

        // 3. 批量 upsert
        if (results.Count > 0)
        {
            await optRepo.BulkUpsertIvGreeksAsync(results);
        }

        // 4. Parity 校验
        var parityResult = validationService.ValidateParity(
            contracts, results, ulPrice, 0.02, tradeDate);
        if (!parityResult.IsValid)
        {
            logger.LogWarning(
                "Parity validation anomalies ({Count}): {Sample}",
                parityResult.Anomalies.Count,
                string.Join(" | ", parityResult.Anomalies.Take(5)));
        }

        // 5. IV 百分位缓存刷新
        await percentileService.CalculateAndStoreAsync(task.Underlying, tradeDate);

        sw.Stop();
        logger.LogInformation(
            "Calc completed for {Underlying} {TradeDate}: total={Total}, calc={Calc}, skip={Skip}, anomaly={Anomaly}, elapsed={ElapsedMs}ms",
            task.Underlying, tradeDate.ToString("yyyy-MM-dd"),
            dailyData.Count, calculated, skipped, anomalies,
            sw.ElapsedMilliseconds);
    }
}