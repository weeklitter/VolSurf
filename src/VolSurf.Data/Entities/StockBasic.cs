namespace VolSurf.Data.Entities;

/// <summary>股票基础信息</summary>
public class StockBasic
{
    public string TsCode { get; set; } = default!;      // 股票代码，如 600519.SH
    public string Symbol { get; set; } = default!;      // 简码，如 600519
    public string Name { get; set; } = default!;        // 股票名称，如 贵州茅台
    public string? Area { get; set; }                   // 地域
    public string? Industry { get; set; }               // 所属行业
    public string? Market { get; set; }                 // 市场类型（主板/创业板/科创板）
    public DateTime? ListDate { get; set; }             // 上市日期
    public string Exchange { get; set; } = default!;    // 交易所 SSE/SZSE/BSE
}
