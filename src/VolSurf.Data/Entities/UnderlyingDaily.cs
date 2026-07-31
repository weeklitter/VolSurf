namespace VolSurf.Data.Entities;

public class UnderlyingDaily
{
    public string TsCode { get; set; } = default!;
    public DateTime TradeDate { get; set; }
    public decimal Close { get; set; }
}