namespace VolSurf.Core.Models.Dto;

/// <summary>期权链返回 DTO</summary>
public class OptionChainDto
{
    public UnderlyingInfo Underlying { get; set; } = default!;
    public string TradeDate { get; set; } = default!;
    public string Expiry { get; set; } = default!;
    public decimal? IvPercentile { get; set; }
    public List<OptionContractDto> Calls { get; set; } = new();
    public List<OptionContractDto> Puts { get; set; } = new();
}

public class UnderlyingInfo
{
    public string TsCode { get; set; } = default!;
    public string Name { get; set; } = default!;
    public decimal Price { get; set; }
}

public class OptionContractDto
{
    public string TsCode { get; set; } = default!;
    public decimal Strike { get; set; }
    public decimal? Price { get; set; }
    public decimal? Settle { get; set; }
    public decimal? Volume { get; set; }
    public decimal? OpenInterest { get; set; }
    public decimal? Iv { get; set; }
    public decimal? Delta { get; set; }
    public bool IvConfidence { get; set; }
}