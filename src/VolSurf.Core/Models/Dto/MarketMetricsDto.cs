namespace VolSurf.Core.Models.Dto;

/// <summary>市场表现</summary>
public class MarketMetricsDto
{
    public double Price { get; set; }                    // 最新价
    public double PctChg1M { get; set; }                 // 近1月涨跌幅
    public double PctChg3M { get; set; }                 // 近3月涨跌幅
    public double PctChg1Y { get; set; }                 // 近1年涨跌幅
    public double PctChgYTD { get; set; }                // 年初至今涨跌幅
    public double VsHs3001Y { get; set; }                // 相对沪深300超额收益(1年)
    public double Ma20 { get; set; }
    public double Ma60 { get; set; }
    public double Ma120 { get; set; }
    public double Ma250 { get; set; }
    public string MaTrend { get; set; } = "mixed";       // bull/bear/mixed
    public double Volatility { get; set; }               // 近60日年化波动率
    public List<PricePointDto> PriceTrend { get; set; } = new();
}

/// <summary>价格数据点（用于K线图）</summary>
public class PricePointDto
{
    public string Date { get; set; } = default!;         // YYYY-MM-DD
    public double Open { get; set; }
    public double High { get; set; }
    public double Low { get; set; }
    public double Close { get; set; }
    public double Volume { get; set; }
    public double? Ma20 { get; set; }
    public double? Ma60 { get; set; }
    public double? Ma120 { get; set; }
}
