namespace VolSurf.Data.Entities;

public class Underlying
{
    public string TsCode { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string Exchange { get; set; } = default!;
    public string AssetClass { get; set; } = default!;  // ETF/INDEX
    public int SortOrder { get; set; }
}