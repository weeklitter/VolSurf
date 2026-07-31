using VolSurf.Data.Entities;

namespace VolSurf.Data.Repositories;

public interface IOptionRepository
{
    // ── 合约查询 ──
    /// <summary>获取某标的当日活跃合约（未到期）</summary>
    Task<List<OptionContract>> GetActiveContractsAsync(string underlying, DateTime? tradeDate = null);

    /// <summary>获取某标的指定到期月的合约</summary>
    Task<List<OptionContract>> GetContractsByExpiryAsync(string underlying, DateTime expiry);

    /// <summary>获取某标的可用的所有到期月</summary>
    Task<List<DateTime>> GetAvailableExpiriesAsync(string underlying, DateTime tradeDate);

    // ── 日线行情 ──
    /// <summary>获取某交易日全量期权日线（可选按标的过滤）</summary>
    Task<List<OptionDaily>> GetOptionDailyAsync(DateTime tradeDate, string? underlying = null);

    /// <summary>获取某合约某交易日日线</summary>
    Task<OptionDaily?> GetOptionDailyAsync(string tsCode, DateTime tradeDate);

    /// <summary>获取某合约前一交易日日线（用于价格偏离校验）</summary>
    Task<OptionDaily?> GetPreviousOptionDailyAsync(string tsCode, DateTime tradeDate);

    /// <summary>批量 upsert 期权日线</summary>
    Task BulkUpsertOptionDailyAsync(IEnumerable<OptionDaily> records);

    // ── IV/Greeks ──
    /// <summary>获取某标的某交易日的IV/Greeks数据</summary>
    Task<List<IvGreeks>> GetIvGreeksAsync(DateTime tradeDate, string underlying);

    /// <summary>获取某标的过去N天的IV/Greeks历史数据</summary>
    Task<List<IvGreeks>> GetIvGreeksHistoryAsync(string underlying, int days);

    /// <summary>批量 upsert IV/Greeks（upsert语义，重复计算覆盖）</summary>
    Task BulkUpsertIvGreeksAsync(IEnumerable<IvGreeks> records);

    // ── 交易日 ──
    /// <summary>获取某标的可用交易日列表（降序）</summary>
    Task<List<DateTime>> GetTradeDatesAsync(string underlying, int limit = 30);

    /// <summary>获取某标的最新有数据的交易日</summary>
    Task<DateTime?> GetLatestTradeDateAsync(string underlying);

    // ── 合约信息 upsert ──
    /// <summary>批量 upsert 合约信息</summary>
    Task BulkUpsertContractsAsync(IEnumerable<OptionContract> records);
}