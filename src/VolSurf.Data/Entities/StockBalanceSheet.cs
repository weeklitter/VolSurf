namespace VolSurf.Data.Entities;

/// <summary>资产负债表</summary>
public class StockBalanceSheet
{
    public string TsCode { get; set; } = default!;
    public DateTime EndDate { get; set; }
    public string ReportType { get; set; } = default!;
    public decimal? TotalAssets { get; set; }           // 总资产
    public decimal? TotalLiab { get; set; }             // 总负债
    public decimal? TotalEquity { get; set; }           // 股东权益
    public decimal? Goodwill { get; set; }              // 商誉
    public decimal? AccountRecv { get; set; }           // 应收账款
    public decimal? Inventory { get; set; }             // 存货
    public DateTime UpdateDate { get; set; }
}
