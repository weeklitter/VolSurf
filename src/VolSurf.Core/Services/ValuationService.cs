using VolSurf.Core.Models.Dto;
using VolSurf.Data.Repositories;

namespace VolSurf.Core.Services;

/// <summary>
/// 估值分位计算服务。
///
/// 从 StockDailyBasic 表获取近5年的 PE/PB/PS 序列，
/// 计算当前值在历史序列中的百分位排名。
///
/// 分位数计算：percentile = 排名数 / 总数 × 100
/// 分位数 < 30% = undervalued（低估）
/// 30-70% = fair（合理）
/// > 70% = overvalued（高估）
/// </summary>
public class ValuationService(IStockRepository repo)
{
    // 5年交易日数约 250*5 = 1250
    private const int LookbackDays = 1250;

    /// <summary>获取估值指标完整报告</summary>
    public async Task<ValuationMetricsDto> GetValuationMetricsAsync(string tsCode)
    {
        // 1. 获取最新每日指标
        var latest = await repo.GetLatestDailyBasicAsync(tsCode);
        if (latest == null)
            return new ValuationMetricsDto();

        // 2. 获取近5年历史序列
        var history = await repo.GetDailyBasicHistoryAsync(tsCode, LookbackDays);

        // 3. 计算各估值指标分位
        var pe = CalcValuationItem(history, latest.Pe, latest.PeTtm, "PE", h => h.Pe);
        var peTtm = CalcValuationItem(history, latest.PeTtm, latest.PeTtm, "PE TTM", h => h.PeTtm);
        var pb = CalcValuationItem(history, latest.Pb, latest.Pb, "PB", h => h.Pb);
        var ps = CalcValuationItem(history, latest.Ps, latest.PsTtm, "PS", h => h.Ps);

        // 4. 股息率
        var dvRatio = CalcDvRatio(latest.DvRatio, history);

        // 5. 计算估值评分
        double? pePercentile = pe?.Percentile5y;
        double? pbPercentile = pb?.Percentile5y;
        double valueScore = ScoreEngine.CalculateValueScore(
            pePercentile, pbPercentile, latest.DvRatio.HasValue ? (double)latest.DvRatio : null);

        return new ValuationMetricsDto
        {
            Pe = pe,
            PeTtm = peTtm,
            Pb = pb,
            Ps = ps,
            TotalMv = latest.TotalMv.HasValue ? (double)latest.TotalMv : null,
            DvRatio = dvRatio,
            ValueScore = Math.Round(valueScore, 1)
        };
    }

    /// <summary>计算单个估值指标的分位信息</summary>
    private ValuationItemDto? CalcValuationItem(
        List<Data.Entities.StockDailyBasic> history,
        decimal? currentValue, decimal? ttmValue, string label,
        Func<Data.Entities.StockDailyBasic, decimal?> selector)
    {
        if (!currentValue.HasValue || currentValue <= 0)
            return null;

        var values = history
            .Where(h => selector(h).HasValue && selector(h) > 0) // 过滤负值和零值
            .Select(h => (double)selector(h)!.Value)
            .ToList();

        if (values.Count == 0)
            return new ValuationItemDto
            {
                Value = Math.Round((double)currentValue, 2),
                Label = label,
                Level = "fair"
            };

        double current = (double)currentValue;
        double percentile = (double)values.Count(v => v <= current) / values.Count * 100;

        // 排序计算中位数、最小值、最大值
        values.Sort();
        double median = values[values.Count / 2];
        double min = values[0];
        double max = values[^1];

        string level = percentile switch
        {
            < 30 => "undervalued",
            <= 70 => "fair",
            _ => "overvalued"
        };

        return new ValuationItemDto
        {
            Value = Math.Round(current, 2),
            Percentile5y = Math.Round(percentile, 1),
            Median5y = Math.Round(median, 2),
            Min5y = Math.Round(min, 2),
            Max5y = Math.Round(max, 2),
            Level = level,
            Label = label
        };
    }

    /// <summary>股息率评分</summary>
    private MetricItemDto? CalcDvRatio(
        decimal? dvRatio, List<Data.Entities.StockDailyBasic> history)
    {
        if (!dvRatio.HasValue)
            return null;

        double value = (double)dvRatio.Value;
        double score = ScoreEngine.LinearInterpolate(value, ScoreEngine.DvRatioThresholds);

        return new MetricItemDto
        {
            Value = Math.Round(value, 2),
            Score = Math.Round(score, 1),
            Level = ScoreEngine.GetLevel(score),
            Label = "股息率",
            Unit = "%",
            Description = "近12个月股息率"
        };
    }
}
