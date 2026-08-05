using Microsoft.EntityFrameworkCore;
using VolSurf.Data.Entities;

namespace VolSurf.Data.Repositories;

/// <summary>
/// 股票数据访问实现。
/// 所有查询使用 EF Core LINQ，参照现有 OptionRepository 风格。
/// </summary>
public class StockRepository(VolSurfDbContext db) : IStockRepository
{
    // ═══════════════════════════════════════════════════════════════════════════
    // 股票基础信息
    // ═══════════════════════════════════════════════════════════════════════════

    public Task<StockBasic?> GetStockBasicAsync(string tsCode)
    {
        return db.StockBasic.FirstOrDefaultAsync(x => x.TsCode == tsCode);
    }

    public async Task<List<StockBasic>> SearchStocksAsync(string keyword, int limit = 20)
    {
        // 模糊搜索：代码或名称包含关键词
        var kw = keyword.Trim();
        return await db.StockBasic
            .Where(x => x.TsCode.Contains(kw) || x.Name.Contains(kw) || x.Symbol.Contains(kw))
            .Take(limit)
            .ToListAsync();
    }

    public async Task<(List<StockBasic> stocks, int total)> GetStockListAsync(
        string? industry = null, int page = 1, int size = 20)
    {
        var q = db.StockBasic.AsQueryable();
        if (!string.IsNullOrEmpty(industry))
            q = q.Where(x => x.Industry == industry);

        var total = await q.CountAsync();
        var stocks = await q
            .OrderBy(x => x.TsCode)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

        return (stocks, total);
    }

    public async Task<List<string>> GetIndustriesAsync()
    {
        return await db.StockBasic
            .Where(x => x.Industry != null)
            .Select(x => x.Industry!)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 日线行情
    // ═══════════════════════════════════════════════════════════════════════════

    public async Task<List<StockDaily>> GetStockDailyAsync(string tsCode, DateTime start, DateTime end)
    {
        return await db.StockDaily
            .Where(x => x.TsCode == tsCode && x.TradeDate >= start.Date && x.TradeDate <= end.Date)
            .OrderBy(x => x.TradeDate)
            .ToListAsync();
    }

    public async Task<List<StockDaily>> GetRecentStockDailyAsync(string tsCode, int days)
    {
        return await db.StockDaily
            .Where(x => x.TsCode == tsCode)
            .OrderByDescending(x => x.TradeDate)
            .Take(days)
            .OrderBy(x => x.TradeDate)  // 再按时间正序返回（便于画K线）
            .ToListAsync();
    }

    public async Task<StockDaily?> GetLatestStockDailyAsync(string tsCode)
    {
        return await db.StockDaily
            .Where(x => x.TsCode == tsCode)
            .OrderByDescending(x => x.TradeDate)
            .FirstOrDefaultAsync();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 利润表
    // ═══════════════════════════════════════════════════════════════════════════

    public async Task<List<StockIncome>> GetIncomeStatementsAsync(string tsCode, int quarters = 8)
    {
        return await db.StockIncome
            .Where(x => x.TsCode == tsCode && x.ReportType == "1")
            .OrderByDescending(x => x.EndDate)
            .Take(quarters)
            .OrderBy(x => x.EndDate)  // 正序返回（便于画趋势图）
            .ToListAsync();
    }

    public async Task<StockIncome?> GetIncomeStatementAsync(
        string tsCode, DateTime endDate, string reportType = "1")
    {
        return await db.StockIncome
            .FirstOrDefaultAsync(x => x.TsCode == tsCode
                && x.EndDate == endDate.Date
                && x.ReportType == reportType);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 资产负债表
    // ═══════════════════════════════════════════════════════════════════════════

    public async Task<List<StockBalanceSheet>> GetBalanceSheetsAsync(string tsCode, int quarters = 8)
    {
        return await db.StockBalanceSheet
            .Where(x => x.TsCode == tsCode && x.ReportType == "1")
            .OrderByDescending(x => x.EndDate)
            .Take(quarters)
            .OrderBy(x => x.EndDate)
            .ToListAsync();
    }

    public async Task<StockBalanceSheet?> GetBalanceSheetAsync(
        string tsCode, DateTime endDate, string reportType = "1")
    {
        return await db.StockBalanceSheet
            .FirstOrDefaultAsync(x => x.TsCode == tsCode
                && x.EndDate == endDate.Date
                && x.ReportType == reportType);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 现金流量表
    // ═══════════════════════════════════════════════════════════════════════════

    public async Task<List<StockCashflow>> GetCashflowStatementsAsync(string tsCode, int quarters = 8)
    {
        return await db.StockCashflow
            .Where(x => x.TsCode == tsCode && x.ReportType == "1")
            .OrderByDescending(x => x.EndDate)
            .Take(quarters)
            .OrderBy(x => x.EndDate)
            .ToListAsync();
    }

    public async Task<StockCashflow?> GetCashflowStatementAsync(
        string tsCode, DateTime endDate, string reportType = "1")
    {
        return await db.StockCashflow
            .FirstOrDefaultAsync(x => x.TsCode == tsCode
                && x.EndDate == endDate.Date
                && x.ReportType == reportType);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 主营业务构成
    // ═══════════════════════════════════════════════════════════════════════════

    public async Task<List<StockBusiness>> GetBusinessCompositionAsync(
        string tsCode, string? mainType = null)
    {
        // 取最新报告期
        var latestEndDate = await db.StockBusiness
            .Where(x => x.TsCode == tsCode)
            .MaxAsync(x => (DateTime?)x.EndDate);

        if (latestEndDate == null) return new List<StockBusiness>();

        var q = db.StockBusiness
            .Where(x => x.TsCode == tsCode && x.EndDate == latestEndDate.Value);

        if (!string.IsNullOrEmpty(mainType))
            q = q.Where(x => x.MainType == mainType);

        return await q.OrderByDescending(x => x.Revenue).ToListAsync();
    }

    public async Task<List<StockBusiness>> GetBusinessCompositionHistoryAsync(
        string tsCode, int quarters = 4)
    {
        var endDates = await db.StockBusiness
            .Where(x => x.TsCode == tsCode)
            .Select(x => x.EndDate)
            .Distinct()
            .OrderByDescending(x => x)
            .Take(quarters)
            .ToListAsync();

        if (endDates.Count == 0) return new List<StockBusiness>();

        return await db.StockBusiness
            .Where(x => x.TsCode == tsCode && endDates.Contains(x.EndDate))
            .OrderBy(x => x.EndDate)
            .ThenByDescending(x => x.Revenue)
            .ToListAsync();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 每日指标（估值）
    // ═══════════════════════════════════════════════════════════════════════════

    public async Task<StockDailyBasic?> GetLatestDailyBasicAsync(string tsCode)
    {
        return await db.StockDailyBasic
            .Where(x => x.TsCode == tsCode)
            .OrderByDescending(x => x.TradeDate)
            .FirstOrDefaultAsync();
    }

    public async Task<List<StockDailyBasic>> GetDailyBasicHistoryAsync(string tsCode, int days)
    {
        // 约5年 = 250*5 = 1250 个交易日
        return await db.StockDailyBasic
            .Where(x => x.TsCode == tsCode)
            .OrderByDescending(x => x.TradeDate)
            .Take(days)
            .OrderBy(x => x.TradeDate)
            .ToListAsync();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 沪深300基准
    // ═══════════════════════════════════════════════════════════════════════════

    public async Task<List<StockDaily>> GetHs300DailyAsync(int days)
    {
        // 沪深300指数在 stock_daily 表中的 ts_code = "000300.SH"
        return await db.StockDaily
            .Where(x => x.TsCode == "000300.SH")
            .OrderByDescending(x => x.TradeDate)
            .Take(days)
            .OrderBy(x => x.TradeDate)
            .ToListAsync();
    }
}
