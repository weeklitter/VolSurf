using Microsoft.EntityFrameworkCore;
using VolSurf.Data.Entities;

namespace VolSurf.Data.Repositories;

public class OptionRepository(VolSurfDbContext db) : IOptionRepository
{
    public Task<List<OptionContract>> GetActiveContractsAsync(string underlying, DateTime? tradeDate = null)
    {
        var q = db.OptionContracts.AsQueryable().Where(c => c.Underlying == underlying);
        if (tradeDate.HasValue)
        {
            var d = tradeDate.Value.Date;
            q = q.Where(c => c.MaturityDate > d
                              && (c.DelistDate == null || c.DelistDate > d)
                              && (c.ListDate == null || c.ListDate <= d));
        }
        else
        {
            q = q.Where(c => c.MaturityDate > DateTime.UtcNow.Date);
        }
        return q.OrderBy(c => c.MaturityDate).ThenBy(c => c.CallPut).ThenBy(c => c.ExercisePrice).ToListAsync();
    }

    public Task<List<OptionContract>> GetContractsByExpiryAsync(string underlying, DateTime expiry)
    {
        return db.OptionContracts
            .Where(c => c.Underlying == underlying && c.MaturityDate == expiry.Date)
            .OrderBy(c => c.CallPut).ThenBy(c => c.ExercisePrice)
            .ToListAsync();
    }

    public async Task<List<DateTime>> GetAvailableExpiriesAsync(string underlying, DateTime tradeDate)
    {
        var d = tradeDate.Date;
        return await db.OptionContracts
            .Where(c => c.Underlying == underlying
                        && c.MaturityDate > d
                        && (c.DelistDate == null || c.DelistDate > d)
                        && (c.ListDate == null || c.ListDate <= d))
            .Select(c => c.MaturityDate)
            .Distinct()
            .OrderBy(t => t)
            .ToListAsync();
    }

    public Task<List<OptionDaily>> GetOptionDailyAsync(DateTime tradeDate, string? underlying = null)
    {
        var q = db.OptionDaily.Where(d => d.TradeDate == tradeDate.Date);
        if (!string.IsNullOrEmpty(underlying))
            q = q.Where(d => d.Underlying == underlying);
        return q.ToListAsync();
    }

    public Task<OptionDaily?> GetOptionDailyAsync(string tsCode, DateTime tradeDate)
    {
        return db.OptionDaily.FirstOrDefaultAsync(d => d.TsCode == tsCode && d.TradeDate == tradeDate.Date);
    }

    public async Task<OptionDaily?> GetPreviousOptionDailyAsync(string tsCode, DateTime tradeDate)
    {
        return await db.OptionDaily
            .Where(d => d.TsCode == tsCode && d.TradeDate < tradeDate.Date)
            .OrderByDescending(d => d.TradeDate)
            .FirstOrDefaultAsync();
    }

    public async Task BulkUpsertOptionDailyAsync(IEnumerable<OptionDaily> records)
    {
        var list = records.ToList();
        if (list.Count == 0) return;

        // Use raw SQL upsert via Npgsql for high-performance bulk write
        const string sql = @"
            INSERT INTO options_daily (ts_code, trade_date, underlying, open, high, low, close, settle, vol, amount, oi)
            VALUES (@ts_code, @trade_date, @underlying, @open, @high, @low, @close, @settle, @vol, @amount, @oi)
            ON CONFLICT (ts_code, trade_date) DO UPDATE SET
                underlying = EXCLUDED.underlying,
                open = EXCLUDED.open,
                high = EXCLUDED.high,
                low = EXCLUDED.low,
                close = EXCLUDED.close,
                settle = EXCLUDED.settle,
                vol = EXCLUDED.vol,
                amount = EXCLUDED.amount,
                oi = EXCLUDED.oi;";

        foreach (var r in list)
        {
            await db.Database.ExecuteSqlRawAsync(sql,
                new Npgsql.NpgsqlParameter("ts_code", r.TsCode),
                new Npgsql.NpgsqlParameter("trade_date", r.TradeDate.Date),
                new Npgsql.NpgsqlParameter("underlying", r.Underlying ?? string.Empty),
                new Npgsql.NpgsqlParameter("open", (object?)r.Open ?? DBNull.Value),
                new Npgsql.NpgsqlParameter("high", (object?)r.High ?? DBNull.Value),
                new Npgsql.NpgsqlParameter("low", (object?)r.Low ?? DBNull.Value),
                new Npgsql.NpgsqlParameter("close", (object?)r.Close ?? DBNull.Value),
                new Npgsql.NpgsqlParameter("settle", (object?)r.Settle ?? DBNull.Value),
                new Npgsql.NpgsqlParameter("vol", (object?)r.Vol ?? DBNull.Value),
                new Npgsql.NpgsqlParameter("amount", (object?)r.Amount ?? DBNull.Value),
                new Npgsql.NpgsqlParameter("oi", (object?)r.Oi ?? DBNull.Value));
        }
    }

    public Task<List<IvGreeks>> GetIvGreeksAsync(DateTime tradeDate, string underlying)
    {
        return db.IvGreeks
            .Where(x => x.TradeDate == tradeDate.Date && x.Underlying == underlying)
            .ToListAsync();
    }

    public async Task<List<IvGreeks>> GetIvGreeksHistoryAsync(string underlying, int days)
    {
        var cutoff = DateTime.UtcNow.Date.AddDays(-days);
        return await db.IvGreeks
            .Where(x => x.Underlying == underlying && x.TradeDate >= cutoff)
            .OrderBy(x => x.TradeDate)
            .ToListAsync();
    }

    public async Task BulkUpsertIvGreeksAsync(IEnumerable<IvGreeks> records)
    {
        var list = records.ToList();
        if (list.Count == 0) return;

        // 表实际列名为 PascalCase（与 EF Core 实体属性一致）
        const string sql = @"
            INSERT INTO options_iv_greeks (""TsCode"", ""TradeDate"", ""Underlying"", ""Iv"", ""Delta"", ""Gamma"", ""Theta"", ""Vega"", ""Rho"", ""IvConfidence"", ""IvAnomaly"")
            VALUES (@ts_code, @trade_date, @underlying, @iv, @delta, @gamma, @theta, @vega, @rho, @iv_confidence, @iv_anomaly)
            ON CONFLICT (""TsCode"", ""TradeDate"") DO UPDATE SET
                ""Underlying"" = EXCLUDED.""Underlying"",
                ""Iv"" = EXCLUDED.""Iv"",
                ""Delta"" = EXCLUDED.""Delta"",
                ""Gamma"" = EXCLUDED.""Gamma"",
                ""Theta"" = EXCLUDED.""Theta"",
                ""Vega"" = EXCLUDED.""Vega"",
                ""Rho"" = EXCLUDED.""Rho"",
                ""IvConfidence"" = EXCLUDED.""IvConfidence"",
                ""IvAnomaly"" = EXCLUDED.""IvAnomaly"";";

        foreach (var r in list)
        {
            await db.Database.ExecuteSqlRawAsync(sql,
                new Npgsql.NpgsqlParameter("ts_code", r.TsCode),
                new Npgsql.NpgsqlParameter("trade_date", r.TradeDate.Date),
                new Npgsql.NpgsqlParameter("underlying", r.Underlying ?? string.Empty),
                new Npgsql.NpgsqlParameter("iv", (object?)r.Iv ?? DBNull.Value),
                new Npgsql.NpgsqlParameter("delta", (object?)r.Delta ?? DBNull.Value),
                new Npgsql.NpgsqlParameter("gamma", (object?)r.Gamma ?? DBNull.Value),
                new Npgsql.NpgsqlParameter("theta", (object?)r.Theta ?? DBNull.Value),
                new Npgsql.NpgsqlParameter("vega", (object?)r.Vega ?? DBNull.Value),
                new Npgsql.NpgsqlParameter("rho", (object?)r.Rho ?? DBNull.Value),
                new Npgsql.NpgsqlParameter("iv_confidence", r.IvConfidence),
                new Npgsql.NpgsqlParameter("iv_anomaly", r.IvAnomaly));
        }
    }

    public async Task<List<DateTime>> GetTradeDatesAsync(string underlying, int limit = 30)
    {
        return await db.OptionDaily
            .Where(d => d.Underlying == underlying)
            .Select(d => d.TradeDate)
            .Distinct()
            .OrderByDescending(t => t)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<DateTime?> GetLatestTradeDateAsync(string underlying)
    {
        return await db.OptionDaily
            .Where(d => d.Underlying == underlying)
            .Select(d => (DateTime?)d.TradeDate)
            .DefaultIfEmpty()
            .MaxAsync();
    }

    public async Task<UnderlyingDaily?> GetLatestUnderlyingDailyAsync(string underlying)
    {
        return await db.UnderlyingDaily
            .Where(d => d.TsCode == underlying)
            .OrderByDescending(d => d.TradeDate)
            .FirstOrDefaultAsync();
    }

    public async Task BulkUpsertContractsAsync(IEnumerable<OptionContract> records)
    {
        var list = records.ToList();
        if (list.Count == 0) return;

        const string sql = @"
            INSERT INTO options_contracts (ts_code, symbol, exchange, name, underlying, call_put,
                                           exercise_price, exercise_type, opt_multiplier,
                                           maturity_date, list_date, delist_date, adjusted,
                                           created_at, updated_at)
            VALUES (@ts_code, @symbol, @exchange, @name, @underlying, @call_put,
                    @exercise_price, @exercise_type, @opt_multiplier,
                    @maturity_date, @list_date, @delist_date, @adjusted,
                    NOW(), NOW())
            ON CONFLICT (ts_code) DO UPDATE SET
                symbol = EXCLUDED.symbol,
                exchange = EXCLUDED.exchange,
                name = EXCLUDED.name,
                underlying = EXCLUDED.underlying,
                call_put = EXCLUDED.call_put,
                exercise_price = EXCLUDED.exercise_price,
                exercise_type = EXCLUDED.exercise_type,
                opt_multiplier = EXCLUDED.opt_multiplier,
                maturity_date = EXCLUDED.maturity_date,
                adjusted = EXCLUDED.adjusted,
                updated_at = NOW();";

        foreach (var r in list)
        {
            await db.Database.ExecuteSqlRawAsync(sql,
                new Npgsql.NpgsqlParameter("ts_code", r.TsCode),
                new Npgsql.NpgsqlParameter("symbol", r.Symbol ?? string.Empty),
                new Npgsql.NpgsqlParameter("exchange", r.Exchange ?? string.Empty),
                new Npgsql.NpgsqlParameter("name", r.Name ?? string.Empty),
                new Npgsql.NpgsqlParameter("underlying", r.Underlying ?? string.Empty),
                new Npgsql.NpgsqlParameter("call_put", r.CallPut ?? string.Empty),
                new Npgsql.NpgsqlParameter("exercise_price", r.ExercisePrice),
                new Npgsql.NpgsqlParameter("exercise_type", r.ExerciseType ?? "欧式"),
                new Npgsql.NpgsqlParameter("opt_multiplier", r.OptMultiplier),
                new Npgsql.NpgsqlParameter("maturity_date", r.MaturityDate.Date),
                new Npgsql.NpgsqlParameter("list_date", (object?)r.ListDate ?? DBNull.Value),
                new Npgsql.NpgsqlParameter("delist_date", (object?)r.DelistDate ?? DBNull.Value),
                new Npgsql.NpgsqlParameter("adjusted", r.Adjusted));
        }
    }
}