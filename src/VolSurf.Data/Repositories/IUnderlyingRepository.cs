using VolSurf.Data.Entities;

namespace VolSurf.Data.Repositories;

public interface IUnderlyingRepository
{
    /// <summary>获取所有标的</summary>
    Task<List<Underlying>> GetAllUnderlyingsAsync();

    /// <summary>获取单个标的</summary>
    Task<Underlying?> GetUnderlyingAsync(string tsCode);

    /// <summary>获取标的某日收盘价</summary>
    Task<UnderlyingDaily?> GetUnderlyingDailyAsync(string tsCode, DateTime tradeDate);

    /// <summary>获取标的最新收盘价</summary>
    Task<UnderlyingDaily?> GetLatestUnderlyingDailyAsync(string tsCode);

    /// <summary>批量 upsert 标的日线</summary>
    Task BulkUpsertUnderlyingDailyAsync(IEnumerable<UnderlyingDaily> records);

    // ── IV百分位缓存 ──
    /// <summary>获取某标的最新IV百分位</summary>
    Task<IvPercentileCache?> GetIvPercentileAsync(string underlying);

    /// <summary>获取某标的过去N天的ATM IV序列</summary>
    Task<List<IvPercentileCache>> GetIvPercentileHistoryAsync(string underlying, int days);

    /// <summary>upsert IV百分位缓存</summary>
    Task UpsertIvPercentileAsync(IvPercentileCache record);
}