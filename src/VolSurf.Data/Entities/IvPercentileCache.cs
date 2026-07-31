namespace VolSurf.Data.Entities;

public class IvPercentileCache
{
    public string Underlying { get; set; } = default!;
    public DateTime TradeDate { get; set; }
    public decimal? AtmIv { get; set; }
    public decimal? IvPercentile { get; set; }
    public decimal? IvMean { get; set; }
    public decimal? IvStd { get; set; }
    public int? SampleDays { get; set; }
}