using Microsoft.EntityFrameworkCore;
using VolSurf.Data.Entities;

namespace VolSurf.Data.Repositories;

public class UnderlyingRepository(VolSurfDbContext db) : IUnderlyingRepository
{
    public Task<List<Underlying>> GetAllUnderlyingsAsync()
    {
        return db.Underlyings.OrderBy(u => u.SortOrder).ToListAsync();
    }

    public Task<Underlying?> GetUnderlyingAsync(string tsCode)
    {
        return db.Underlyings.FirstOrDefaultAsync(u => u.TsCode == tsCode);
    }

    public Task<UnderlyingDaily?> GetUnderlyingDailyAsync(string tsCode, DateTime tradeDate)
    {
        return db.UnderlyingDaily.FirstOrDefaultAsync(d => d.TsCode == tsCode && d.TradeDate == tradeDate.Date);
    }

    public async Task<UnderlyingDaily?> GetLatestUnderlyingDailyAsync(string tsCode)
    {
        return await db.UnderlyingDaily
            .Where(d => d.TsCode == tsCode)
            .OrderByDescending(d => d.TradeDate)
            .FirstOrDefaultAsync();
    }

    public async Task BulkUpsertUnderlyingDailyAsync(IEnumerable<UnderlyingDaily> records)
    {
        var list = records.ToList();
        if (list.Count == 0) return;

        const string sql = @"
            INSERT INTO underlying_daily (ts_code, trade_date, close)
            VALUES (@ts_code, @trade_date, @close)
            ON CONFLICT (ts_code, trade_date) DO UPDATE SET
                close = EXCLUDED.close;";

        foreach (var r in list)
        {
            await db.Database.ExecuteSqlRawAsync(sql,
                new Npgsql.NpgsqlParameter("ts_code", r.TsCode),
                new Npgsql.NpgsqlParameter("trade_date", r.TradeDate.Date),
                new Npgsql.NpgsqlParameter("close", r.Close));
        }
    }

    public async Task<IvPercentileCache?> GetIvPercentileAsync(string underlying)
    {
        return await db.IvPercentileCache
            .Where(p => p.Underlying == underlying)
            .OrderByDescending(p => p.TradeDate)
            .FirstOrDefaultAsync();
    }

    public async Task<List<IvPercentileCache>> GetIvPercentileHistoryAsync(string underlying, int days)
    {
        var cutoff = DateTime.UtcNow.Date.AddDays(-days);
        return await db.IvPercentileCache
            .Where(p => p.Underlying == underlying && p.TradeDate >= cutoff)
            .OrderBy(p => p.TradeDate)
            .ToListAsync();
    }

    public async Task UpsertIvPercentileAsync(IvPercentileCache record)
    {
        const string sql = @"
            INSERT INTO iv_percentile_cache (underlying, trade_date, atm_iv, iv_percentile, iv_mean, iv_std, sample_days)
            VALUES (@underlying, @trade_date, @atm_iv, @iv_percentile, @iv_mean, @iv_std, @sample_days)
            ON CONFLICT (underlying, trade_date) DO UPDATE SET
                atm_iv = EXCLUDED.atm_iv,
                iv_percentile = EXCLUDED.iv_percentile,
                iv_mean = EXCLUDED.iv_mean,
                iv_std = EXCLUDED.iv_std,
                sample_days = EXCLUDED.sample_days;";

        await db.Database.ExecuteSqlRawAsync(sql,
            new Npgsql.NpgsqlParameter("underlying", record.Underlying),
            new Npgsql.NpgsqlParameter("trade_date", record.TradeDate.Date),
            new Npgsql.NpgsqlParameter("atm_iv", (object?)record.AtmIv ?? DBNull.Value),
            new Npgsql.NpgsqlParameter("iv_percentile", (object?)record.IvPercentile ?? DBNull.Value),
            new Npgsql.NpgsqlParameter("iv_mean", (object?)record.IvMean ?? DBNull.Value),
            new Npgsql.NpgsqlParameter("iv_std", (object?)record.IvStd ?? DBNull.Value),
            new Npgsql.NpgsqlParameter("sample_days", (object?)record.SampleDays ?? DBNull.Value));
    }
}