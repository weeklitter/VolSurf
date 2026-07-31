namespace VolSurf.Data.Entities;

public class OptionDaily
{
    public string TsCode { get; set; } = default!;
    public DateTime TradeDate { get; set; }
    public string Underlying { get; set; } = default!;  // 冗余字段
    public decimal? Open { get; set; }
    public decimal? High { get; set; }
    public decimal? Low { get; set; }
    public decimal? Close { get; set; }
    public decimal? Settle { get; set; }    // IV计算用此字段
    public decimal? Vol { get; set; }
    public decimal? Amount { get; set; }
    public decimal? Oi { get; set; }
}