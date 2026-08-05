namespace VolSurf.Core.Models.Dto;

/// <summary>主营业务构成</summary>
public class BusinessCompositionDto
{
    /// <summary>按产品分类</summary>
    public List<BusinessItemDto> ByProduct { get; set; } = new();

    /// <summary>按地区分类</summary>
    public List<BusinessItemDto> ByRegion { get; set; } = new();

    /// <summary>近5年营收趋势（亿元）</summary>
    public List<double> RevenueTrend5y { get; set; } = new();

    /// <summary>报告期</summary>
    public string EndDate { get; set; } = default!;
}

/// <summary>单个业务项</summary>
public class BusinessItemDto
{
    public string Name { get; set; } = default!;         // 业务项名称
    public double Revenue { get; set; }                   // 营业收入(亿元)
    public double? Cost { get; set; }                     // 营业成本(亿元)
    public double? Profit { get; set; }                   // 毛利润(亿元)
    public double? Ratio { get; set; }                    // 占比(%)
    public double? Margin { get; set; }                   // 毛利率(%)
}
