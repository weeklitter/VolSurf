namespace VolSurf.Core.Models.Dto;

/// <summary>期限结构 DTO</summary>
public class TermStructureDto
{
    public string Underlying { get; set; } = default!;
    public string Date { get; set; } = default!;
    public List<TermStructurePoint> Points { get; set; } = new();
}

public class TermStructurePoint
{
    public DateTime Expiry { get; set; }
    public int DaysToExpiry { get; set; }
    public double AtmIv { get; set; }
}