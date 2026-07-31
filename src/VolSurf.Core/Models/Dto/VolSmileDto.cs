namespace VolSurf.Core.Models.Dto;

/// <summary>波动率微笑曲线 DTO</summary>
public class VolSmileDto
{
    public string Underlying { get; set; } = default!;
    public string Expiry { get; set; } = default!;
    public string Date { get; set; } = default!;
    public double AtmIv { get; set; }
    public double Skew25 { get; set; }
    public List<SmilePoint> Calls { get; set; } = new();
    public List<SmilePoint> Puts { get; set; } = new();
}

public class SmilePoint
{
    public double Strike { get; set; }
    public double Iv { get; set; }
    public double Delta { get; set; }
}