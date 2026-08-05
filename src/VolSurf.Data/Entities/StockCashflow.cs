namespace VolSurf.Data.Entities;

/// <summary>现金流量表</summary>
public class StockCashflow
{
    public string TsCode { get; set; } = default!;
    public DateTime EndDate { get; set; }
    public string ReportType { get; set; } = default!;
    public decimal? OperCashFlow { get; set; }          // 经营活动现金流
    public decimal? InvestCashFlow { get; set; }        // 投资活动现金流
    public decimal? FinCashFlow { get; set; }           // 筹资活动现金流
    public decimal? CapEx { get; set; }                 // 资本支出，对应Tushare c_pay_acquisition_const_ppe
    public DateTime UpdateDate { get; set; }
}
