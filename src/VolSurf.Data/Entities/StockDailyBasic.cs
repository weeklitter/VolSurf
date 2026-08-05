namespace VolSurf.Data.Entities;

/// <summary>每日指标（含估值数据）</summary>
public class StockDailyBasic
{
    public string TsCode { get; set; } = default!;
    public DateTime TradeDate { get; set; }
    public decimal? Close { get; set; }                 // 当日收盘价
    public decimal? Pe { get; set; }                    // 市盈率
    public decimal? PeTtm { get; set; }                 // 市盈率TTM
    public decimal? Pb { get; set; }                    // 市净率
    public decimal? Ps { get; set; }                    // 市销率
    public decimal? PsTtm { get; set; }                 // 市销率TTM
    public decimal? TotalMv { get; set; }               // 总市值(万元)
    public decimal? CircMv { get; set; }                // 流通市值(万元)
    public decimal? TurnoverRate { get; set; }          // 换手率(%)
    public decimal? DvRatio { get; set; }               // 股息率(%)
}
