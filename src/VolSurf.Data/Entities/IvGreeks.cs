namespace VolSurf.Data.Entities;

public class IvGreeks
{
    public string TsCode { get; set; } = default!;
    public DateTime TradeDate { get; set; }
    public string Underlying { get; set; } = default!;  // 冗余字段
    public decimal? Iv { get; set; }
    public decimal? Delta { get; set; }
    public decimal? Gamma { get; set; }
    public decimal? Theta { get; set; }     // 每日Theta（已/365）
    public decimal? Vega { get; set; }      // 每1%波动率（已/100）
    public decimal? Rho { get; set; }       // 每1%利率（已/100）
    public bool IvConfidence { get; set; } = true;
    public bool IvAnomaly { get; set; } = false;
}