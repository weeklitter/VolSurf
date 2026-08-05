namespace VolSurf.Data.Entities;

/// <summary>股票日线行情</summary>
public class StockDaily
{
    public string TsCode { get; set; } = default!;
    public DateTime TradeDate { get; set; }
    public decimal? Open { get; set; }
    public decimal? High { get; set; }
    public decimal? Low { get; set; }
    public decimal? Close { get; set; }
    public decimal? PreClose { get; set; }              // 昨收
    public decimal? Change { get; set; }                // 涨跌额
    public decimal? PctChg { get; set; }                // 涨跌幅(%)
    public decimal? Vol { get; set; }                   // 成交量(手)
    public decimal? Amount { get; set; }                // 成交额(千元)
}
