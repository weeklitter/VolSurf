namespace VolSurf.Data.Entities;

/// <summary>利润表</summary>
public class StockIncome
{
    public string TsCode { get; set; } = default!;
    public DateTime EndDate { get; set; }               // 报告期（季度末）
    public string ReportType { get; set; } = default!;  // 报告类型 1=合并 4=调整
    public decimal? Revenue { get; set; }               // 营业收入
    public decimal? OperCost { get; set; }              // 营业成本
    public decimal? GrossProfit { get; set; }           // 毛利润
    public decimal? NetProfit { get; set; }             // 净利润
    public DateTime UpdateDate { get; set; }            // 数据更新日期
}
