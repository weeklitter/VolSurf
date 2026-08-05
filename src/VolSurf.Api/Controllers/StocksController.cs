using Microsoft.AspNetCore.Mvc;
using VolSurf.Core.Models;
using VolSurf.Core.Models.Dto;
using VolSurf.Core.Services;
using VolSurf.Data.Repositories;

namespace VolSurf.Api.Controllers;

/// <summary>
/// 股票分析 API 控制器。
/// 提供5个接口：搜索、分析报告、列表、行情、行业列表。
/// </summary>
[ApiController]
[Route("api")]
public class StocksController(
    IStockRepository stockRepo,
    StockAnalysisService analysisService,
    ValuationService valuationService,
    MarketService marketService,
    WarningService warningService) : ControllerBase
{
    /// <summary>GET /api/stocks/search?q=茅台&amp;limit=20</summary>
    [HttpGet("stocks/search")]
    [ResponseCache(Duration = 300, VaryByQueryKeys = new[] { "q", "limit" })]
    public async Task<IActionResult> SearchStocks([FromQuery] StockSearchQuery query)
    {
        var stocks = await stockRepo.SearchStocksAsync(query.Q, query.Limit);
        var data = stocks.Select(s => new
        {
            tsCode = s.TsCode,
            symbol = s.Symbol,
            name = s.Name,
            industry = s.Industry,
            market = s.Market
        }).ToList();

        return Ok(ApiResponse<object>.Ok(data));
    }

    /// <summary>GET /api/stocks/{tsCode}/analysis（核心接口）</summary>
    [HttpGet("stocks/{tsCode}/analysis")]
    [ResponseCache(Duration = 300, VaryByQueryKeys = new[] { "tsCode" })]
    public async Task<IActionResult> GetAnalysis(string tsCode)
    {
        // 1. 获取股票基础信息
        var basic = await stockRepo.GetStockBasicAsync(tsCode)
            ?? throw new KeyNotFoundException($"未找到股票 {tsCode} 的数据");

        // 2. 逐个获取各维度分析（避免 DbContext 并发冲突）
        var financial = await analysisService.GetFinancialMetricsAsync(tsCode, basic.Industry);
        var valuation = await valuationService.GetValuationMetricsAsync(tsCode);
        var market = await marketService.GetMarketMetricsAsync(tsCode);
        var warnings = await warningService.GetWarningsAsync(tsCode, basic.Industry);

        // 3. 获取业务构成
        var business = await GetBusinessCompositionAsync(tsCode);

        // 4. 计算综合评分
        double healthScore = financial.HealthScore;
        double growthScore = financial.GrowthScore;
        double valueScore = valuation.ValueScore;
        double overallScore = ScoreEngine.CalculateOverallScore(healthScore, growthScore, valueScore);

        // 5. 组装报告
        var report = new StockAnalysisReportDto
        {
            TsCode = tsCode,
            Name = basic.Name,
            Industry = basic.Industry ?? "未知",
            ReportDate = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            Financial = financial,
            Valuation = valuation,
            Market = market,
            Business = business,
            Warnings = warnings,
            HealthScore = Math.Round(healthScore, 1),
            GrowthScore = Math.Round(growthScore, 1),
            ValueScore = Math.Round(valueScore, 1),
            OverallScore = Math.Round(overallScore, 1)
        };

        return Ok(ApiResponse<StockAnalysisReportDto>.Ok(report));
    }

    /// <summary>GET /api/stocks?industry=白酒&amp;page=1&amp;size=20</summary>
    [HttpGet("stocks")]
    [ResponseCache(Duration = 300, VaryByQueryKeys = new[] { "industry", "page", "size" })]
    public async Task<IActionResult> GetStockList([FromQuery] StockListQuery query)
    {
        var (stocks, total) = await stockRepo.GetStockListAsync(query.Industry, query.Page, query.Size);
        var data = new
        {
            stocks = stocks.Select(s => new
            {
                tsCode = s.TsCode,
                name = s.Name,
                industry = s.Industry,
                market = s.Market,
                listDate = s.ListDate?.ToString("yyyy-MM-dd")
            }),
            total,
            page = query.Page,
            size = query.Size
        };

        return Ok(ApiResponse<object>.Ok(data));
    }

    /// <summary>GET /api/stocks/{tsCode}/daily?start=2024-01-01&amp;end=2026-12-31</summary>
    [HttpGet("stocks/{tsCode}/daily")]
    [ResponseCache(Duration = 300, VaryByQueryKeys = new[] { "tsCode", "start", "end" })]
    public async Task<IActionResult> GetStockDaily(string tsCode, [FromQuery] StockDailyQuery query)
    {
        var daily = await stockRepo.GetStockDailyAsync(tsCode, query.Start, query.End);
        var data = daily.Select(d => new
        {
            date = d.TradeDate.ToString("yyyy-MM-dd"),
            open = d.Open, high = d.High, low = d.Low, close = d.Close,
            volume = d.Vol, pctChg = d.PctChg
        }).ToList();

        return Ok(ApiResponse<object>.Ok(data));
    }

    /// <summary>GET /api/stocks/industries</summary>
    [HttpGet("stocks/industries")]
    [ResponseCache(Duration = 3600)]
    public async Task<IActionResult> GetIndustries()
    {
        var industries = await stockRepo.GetIndustriesAsync();
        return Ok(ApiResponse<List<string>>.Ok(industries));
    }

    // ── 辅助：获取业务构成 ──
    private async Task<BusinessCompositionDto> GetBusinessCompositionAsync(string tsCode)
    {
        var byProduct = await stockRepo.GetBusinessCompositionAsync(tsCode, "P");
        var byRegion = await stockRepo.GetBusinessCompositionAsync(tsCode, "D");

        var businessHistory = await stockRepo.GetBusinessCompositionHistoryAsync(tsCode, 4);

        // 近5年营收趋势（从利润表取年度数据）
        var incomes = await stockRepo.GetIncomeStatementsAsync(tsCode, 20); // 近5年=20季度
        var revenueTrend5y = incomes
            .GroupBy(i => i.EndDate.Year)
            .OrderBy(g => g.Key)
            .Select(g => g.OrderByDescending(i => i.EndDate).First())
            .Select(g => g.Revenue.HasValue ? (double)g.Revenue.Value / 1e8 : 0)
            .ToList();

        var latestEndDate = byProduct.FirstOrDefault()?.EndDate
            ?? byRegion.FirstOrDefault()?.EndDate
            ?? DateTime.UtcNow;

        return new BusinessCompositionDto
        {
            ByProduct = byProduct.Select(b => new BusinessItemDto
            {
                Name = b.BusinessItem,
                Revenue = b.Revenue.HasValue ? (double)b.Revenue.Value / 1e8 : 0,
                Cost = b.Cost.HasValue ? (double)b.Cost.Value / 1e8 : null,
                Profit = b.Profit.HasValue ? (double)b.Profit.Value / 1e8 : null,
                Ratio = b.Ratio.HasValue ? (double)b.Ratio.Value : null,
                Margin = b.Profit.HasValue && b.Revenue.HasValue && b.Revenue != 0
                    ? (double)(b.Profit.Value / b.Revenue.Value) * 100 : null
            }).ToList(),
            ByRegion = byRegion.Select(b => new BusinessItemDto
            {
                Name = b.BusinessItem,
                Revenue = b.Revenue.HasValue ? (double)b.Revenue.Value / 1e8 : 0,
                Ratio = b.Ratio.HasValue ? (double)b.Ratio.Value : null
            }).ToList(),
            RevenueTrend5y = revenueTrend5y,
            EndDate = latestEndDate.ToString("yyyy-MM-dd")
        };
    }
}
