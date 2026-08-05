namespace VolSurf.Core.Models.Dto;

/// <summary>估值指标</summary>
public class ValuationMetricsDto
{
    public ValuationItemDto? Pe { get; set; }
    public ValuationItemDto? PeTtm { get; set; }
    public ValuationItemDto? Pb { get; set; }
    public ValuationItemDto? Ps { get; set; }
    public double? TotalMv { get; set; }                 // 总市值(万元)
    public MetricItemDto? DvRatio { get; set; }          // 股息率
    public double ValueScore { get; set; }
}

/// <summary>单个估值指标（含分位信息）</summary>
public class ValuationItemDto
{
    public double Value { get; set; }                    // 当前值
    public double? Percentile5y { get; set; }            // 5年分位数(0-100)
    public double? Median5y { get; set; }                // 5年中位数
    public double? Min5y { get; set; }                   // 5年最小值
    public double? Max5y { get; set; }                   // 5年最大值
    public string Level { get; set; } = "fair";          // undervalued/fair/overvalued
    public string Label { get; set; } = default!;
}
