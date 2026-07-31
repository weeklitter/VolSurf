using VolSurf.Data.Entities;
using VolSurf.Data.Repositories;

namespace VolSurf.Core.Services;

/// <summary>
/// IV 百分位计算逻辑：
///   1. 每日计算当日 ATM IV（moneyness = S/K 最接近 1.0 的合约 IV）
///   2. 存入 iv_percentile_cache 表
///   3. 百分位 = 当日 ATM IV 在过去 N 天 ATM IV 序列中的排名百分位
///
/// 注意：不是比较"同一个合约的 IV 历史"（ATM 合约随时间变化），
/// 而是比较"ATM IV 的历史序列"。
/// </summary>
public class IvPercentileService(
    IUnderlyingRepository ulRepo,
    IOptionRepository optRepo)
{
    private const int LookbackDays = 252;

    public async Task CalculateAndStoreAsync(string underlying, DateTime tradeDate)
    {
        var date = tradeDate.Date;

        // 1. 当日 IV + 合约 + 标的价格
        var ivData = await optRepo.GetIvGreeksAsync(date, underlying);
        var contracts = await optRepo.GetActiveContractsAsync(underlying, date);
        var ul = await ulRepo.GetLatestUnderlyingDailyAsync(underlying);
        double S = ul != null ? (double)ul.Close : 0;

        // 2. 当日 ATM IV
        var validData = (from c in contracts
                         join iv in ivData on c.TsCode equals iv.TsCode
                         where iv.Iv.HasValue && iv.IvConfidence
                         select new
                         {
                             c.ExercisePrice,
                             Iv = iv.Iv!.Value
                         }).ToList();

        if (validData.Count == 0)
        {
            // 当日无有效数据：不更新缓存（保留历史）
            return;
        }

        double atmIv = validData
            .OrderBy(x => S == 0 ? 0 : Math.Abs(S / (double)x.ExercisePrice - 1.0))
            .First().Iv;

        // 3. 历史 ATM IV 序列
        var history = await ulRepo.GetIvPercentileHistoryAsync(underlying, LookbackDays);
        var ivHistory = history
            .Where(h => h.AtmIv.HasValue)
            .Select(h => (double)h.AtmIv!.Value)
            .ToList();
        ivHistory.Add(atmIv);

        int sampleDays = ivHistory.Count;
        if (sampleDays == 0) return;

        // 4. 百分位 + 均值 / 标准差
        int rank = ivHistory.Count(x => x <= atmIv);
        double percentile = (double)rank / ivHistory.Count * 100.0;
        double ivMean = ivHistory.Average();
        double ivStd = Math.Sqrt(ivHistory.Average(x => Math.Pow(x - ivMean, 2)));

        // 5. upsert
        await ulRepo.UpsertIvPercentileAsync(new IvPercentileCache
        {
            Underlying = underlying,
            TradeDate = date,
            AtmIv = (decimal)atmIv,
            IvPercentile = (decimal)percentile,
            IvMean = (decimal)ivMean,
            IvStd = (decimal)ivStd,
            SampleDays = sampleDays
        });
    }
}