namespace VolSurf.Core.Models.Dto;

/// <summary>3D 波动率曲面 DTO</summary>
public class VolSurfaceDto
{
    public string Underlying { get; set; } = default!;
    public string Date { get; set; } = default!;
    public decimal UnderlyingPrice { get; set; }
    public List<string> Expiries { get; set; } = new();
    public List<SurfacePoint> Points { get; set; } = new();
}

/// <summary>曲面数据点（Moneyness = S/K）</summary>
public class SurfacePoint
{
    public double Moneyness { get; set; }   // S/K
    public double TimeToExpiry { get; set; }
    public double Iv { get; set; }
    public double Strike { get; set; }
    public DateTime Expiry { get; set; }
    public string CallPut { get; set; } = default!;
}