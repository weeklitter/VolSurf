using VolSurf.Data.Entities;

namespace VolSurf.Data.Repositories;

/// <summary>
/// 股票数据访问接口。
/// 封装股票模块所有数据库查询，参照现有 IOptionRepository 风格。
/// </summary>
public interface IStockRepository
{
    // ── 股票基础信息 ──

    /// <summary>根据 tsCode 获取股票基础信息</summary>
    Task<StockBasic?> GetStockBasicAsync(string tsCode);

    /// <summary>搜索股票（代码或名称模糊匹配）</summary>
    Task<List<StockBasic>> SearchStocksAsync(string keyword, int limit = 20);

    /// <summary>获取所有股票列表（可按行业过滤，分页）</summary>
    Task<(List<StockBasic> stocks, int total)> GetStockListAsync(
        string? industry = null, int page = 1, int size = 20);

    /// <summary>获取所有行业分类列表</summary>
    Task<List<string>> GetIndustriesAsync();

    // ── 日线行情 ──

    /// <summary>获取某股票指定日期范围的日线行情（用于K线图）</summary>
    Task<List<StockDaily>> GetStockDailyAsync(string tsCode, DateTime start, DateTime end);

    /// <summary>获取某股票最近N个交易日的日线（用于计算均线、波动率等）</summary>
    Task<List<StockDaily>> GetRecentStockDailyAsync(string tsCode, int days);

    /// <summary>获取某股票最新一条日线</summary>
    Task<StockDaily?> GetLatestStockDailyAsync(string tsCode);

    // ── 利润表 ──

    /// <summary>获取某股票近N个季度的利润表（reportType=1 合并报表）</summary>
    Task<List<StockIncome>> GetIncomeStatementsAsync(string tsCode, int quarters = 8);

    /// <summary>获取某股票指定报告期的利润表</summary>
    Task<StockIncome?> GetIncomeStatementAsync(string tsCode, DateTime endDate, string reportType = "1");

    // ── 资产负债表 ──

    /// <summary>获取某股票近N个季度的资产负债表</summary>
    Task<List<StockBalanceSheet>> GetBalanceSheetsAsync(string tsCode, int quarters = 8);

    /// <summary>获取某股票指定报告期的资产负债表</summary>
    Task<StockBalanceSheet?> GetBalanceSheetAsync(string tsCode, DateTime endDate, string reportType = "1");

    // ── 现金流量表 ──

    /// <summary>获取某股票近N个季度的现金流量表</summary>
    Task<List<StockCashflow>> GetCashflowStatementsAsync(string tsCode, int quarters = 8);

    /// <summary>获取某股票指定报告期的现金流量表</summary>
    Task<StockCashflow?> GetCashflowStatementAsync(string tsCode, DateTime endDate, string reportType = "1");

    // ── 主营业务构成 ──

    /// <summary>获取某股票最新报告期的主营业务构成</summary>
    Task<List<StockBusiness>> GetBusinessCompositionAsync(string tsCode, string? mainType = null);

    /// <summary>获取某股票近N个季度的主营业务构成</summary>
    Task<List<StockBusiness>> GetBusinessCompositionHistoryAsync(string tsCode, int quarters = 4);

    // ── 每日指标（估值） ──

    /// <summary>获取某股票最新一条每日指标</summary>
    Task<StockDailyBasic?> GetLatestDailyBasicAsync(string tsCode);

    /// <summary>获取某股票过去N天的每日指标序列（用于计算估值分位数）</summary>
    Task<List<StockDailyBasic>> GetDailyBasicHistoryAsync(string tsCode, int days);

    // ── 沪深300基准 ──

    /// <summary>获取沪深300指数近N天日线（用于计算超额收益）</summary>
    Task<List<StockDaily>> GetHs300DailyAsync(int days);
}
