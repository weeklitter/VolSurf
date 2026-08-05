namespace VolSurf.Core.Models.Dto;

/// <summary>财务指标</summary>
public class FinancialMetricsDto
{
    // 盈利能力
    public MetricItemDto? Roe { get; set; }
    public MetricItemDto? Roa { get; set; }
    public MetricItemDto? GrossMargin { get; set; }
    public MetricItemDto? NetMargin { get; set; }

    // 偿债能力
    public MetricItemDto? DebtRatio { get; set; }

    // 成长能力
    public MetricItemDto? RevenueGrowth { get; set; }
    public MetricItemDto? ProfitGrowth { get; set; }

    // 现金流质量
    public MetricItemDto? OcfToProfit { get; set; }
    public MetricItemDto? FreeCashFlow { get; set; }

    // 风险指标
    public MetricItemDto? GoodwillRatio { get; set; }
    public MetricItemDto? RecvRatio { get; set; }

    // 趋势数据（近8季度）
    public List<double> RevenueTrend { get; set; } = new();
    public List<double> ProfitTrend { get; set; } = new();
    public List<double> RoeTrend { get; set; } = new();

    // 评分
    public double HealthScore { get; set; }
    public double GrowthScore { get; set; }
}

/// <summary>单个指标项</summary>
public class MetricItemDto
{
    public double Value { get; set; }                     // 当前值
    public double? PrevYear { get; set; }                 // 去年同期
    public double? YoyChange { get; set; }               // 同比变化
    public string Trend { get; set; } = "stable";        // up/down/stable
    public double Score { get; set; }                     // 0-100 评分
    public string Level { get; set; } = "normal";        // excellent/good/normal/warn/danger
    public string Label { get; set; } = default!;        // 中文标签
    public string Unit { get; set; } = default!;         // 单位
    public string? Description { get; set; }             // 评分依据说明
}
