using VolSurf.Core.Models.Dto;
using VolSurf.Data.Entities;
using VolSurf.Data.Repositories;

namespace VolSurf.Core.Services;

/// <summary>
/// 股票财务指标分析服务。
///
/// 从 StockIncome + StockBalanceSheet + StockCashflow 三张表联合计算财务指标。
/// 所有指标返回 MetricItem 结构（含当前值、同比变化、评分）。
///
/// 计算公式：
///   ROE = 净利润 / 股东权益 × 100%
///   ROA = 净利润 / 总资产 × 100%
///   毛利率 = 毛利润 / 营业收入 × 100%
///   净利率 = 净利润 / 营业收入 × 100%
///   资产负债率 = 总负债 / 总资产 × 100%
///   FCF = 经营现金流 - CapEx（从 StockCashflow.CapEx 直接取）
///   OCF/净利润 = 经营现金流 / 净利润
///   商誉占比 = 商誉 / 总资产 × 100%
///   应收占比 = 应收账款 / 营业收入 × 100%
/// </summary>
public class StockAnalysisService(IStockRepository repo)
{
    /// <summary>获取财务指标完整报告</summary>
    public async Task<FinancialMetricsDto> GetFinancialMetricsAsync(string tsCode, string? industry)
    {
        // 1. 拉取近8季度三大报表
        var incomes = await repo.GetIncomeStatementsAsync(tsCode, 8);
        var balanceSheets = await repo.GetBalanceSheetsAsync(tsCode, 8);
        var cashflows = await repo.GetCashflowStatementsAsync(tsCode, 8);

        // 2. 最新季度数据
        var latestIncome = incomes.LastOrDefault();
        var latestBs = balanceSheets.LastOrDefault();
        var latestCf = cashflows.LastOrDefault();

        // 3. 去年同期数据（用于同比计算）
        var prevYearIncome = incomes.Count >= 5 ? incomes[^5] : null;

        // 4. 逐指标计算
        var roe = CalcRoe(latestIncome, latestBs, prevYearIncome, latestBs);
        var roa = CalcRoa(latestIncome, latestBs, prevYearIncome);
        var grossMargin = CalcGrossMargin(latestIncome, prevYearIncome);
        var netMargin = CalcNetMargin(latestIncome, prevYearIncome);
        var debtRatio = CalcDebtRatio(latestBs, industry);
        var revenueGrowth = CalcRevenueGrowth(latestIncome, prevYearIncome);
        var profitGrowth = CalcProfitGrowth(latestIncome, prevYearIncome);
        var ocfToProfit = CalcOcfToProfit(latestCf, latestIncome);
        var freeCashFlow = CalcFreeCashFlow(latestCf);
        var goodwillRatio = CalcGoodwillRatio(latestBs);
        var recvRatio = CalcRecvRatio(latestBs, latestIncome);

        // 5. 趋势数据（近8季度序列）
        var revenueTrend = incomes.Select(i => (double)(i.Revenue ?? 0) / 1e8).ToList(); // 转亿元
        var profitTrend = incomes.Select(i => (double)(i.NetProfit ?? 0) / 1e8).ToList();
        var roeTrend = incomes.Zip(balanceSheets, (i, b) =>
        {
            if (i.NetProfit.HasValue && b.TotalEquity.HasValue && b.TotalEquity.Value != 0)
                return (double)(i.NetProfit.Value / b.TotalEquity.Value) * 100;
            return 0.0;
        }).ToList();

        // 6. ROE趋势评分
        double roeTrendScore = ScoreEngine.CalculateRoeTrendScore(roeTrend);

        // 7. 计算综合评分
        double healthScore = ScoreEngine.CalculateHealthScore(
            roe?.Value, grossMargin?.Value, debtRatio?.Value,
            ocfToProfit?.Value, goodwillRatio?.Value, recvRatio?.Value,
            industry);

        double growthScore = ScoreEngine.CalculateGrowthScore(
            revenueGrowth?.Value, profitGrowth?.Value, roeTrendScore);

        return new FinancialMetricsDto
        {
            Roe = roe,
            Roa = roa,
            GrossMargin = grossMargin,
            NetMargin = netMargin,
            DebtRatio = debtRatio,
            RevenueGrowth = revenueGrowth,
            ProfitGrowth = profitGrowth,
            OcfToProfit = ocfToProfit,
            FreeCashFlow = freeCashFlow,
            GoodwillRatio = goodwillRatio,
            RecvRatio = recvRatio,
            RevenueTrend = revenueTrend,
            ProfitTrend = profitTrend,
            RoeTrend = roeTrend,
            HealthScore = Math.Round(healthScore, 1),
            GrowthScore = Math.Round(growthScore, 1)
        };
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 各指标计算（含同比、评分）
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>ROE = 净利润 / 股东权益 × 100%</summary>
    private MetricItemDto? CalcRoe(StockIncome? income, StockBalanceSheet? bs,
        StockIncome? prevIncome, StockBalanceSheet? prevBs)
    {
        if (income?.NetProfit == null || bs?.TotalEquity == null || bs.TotalEquity == 0)
            return null;

        double value = (double)(income.NetProfit.Value / bs.TotalEquity.Value) * 100;

        double? prevValue = null;
        double? yoyChange = null;
        if (prevIncome?.NetProfit != null && prevBs?.TotalEquity != null && prevBs.TotalEquity != 0)
        {
            prevValue = (double)(prevIncome.NetProfit.Value / prevBs.TotalEquity.Value) * 100;
            yoyChange = value - prevValue;
        }

        double score = ScoreEngine.LinearInterpolate(value, ScoreEngine.RoeThresholds);

        return new MetricItemDto
        {
            Value = Math.Round(value, 2),
            PrevYear = prevValue.HasValue ? Math.Round(prevValue.Value, 2) : null,
            YoyChange = yoyChange.HasValue ? Math.Round(yoyChange.Value, 2) : null,
            Trend = ScoreEngine.GetTrend(yoyChange),
            Score = Math.Round(score, 1),
            Level = ScoreEngine.GetLevel(score),
            Label = "ROE",
            Unit = "%",
            Description = "净资产收益率 = 净利润 / 股东权益"
        };
    }

    /// <summary>ROA = 净利润 / 总资产 × 100%</summary>
    private MetricItemDto? CalcRoa(StockIncome? income, StockBalanceSheet? bs, StockIncome? prevIncome)
    {
        if (income?.NetProfit == null || bs?.TotalAssets == null || bs.TotalAssets == 0)
            return null;

        double value = (double)(income.NetProfit.Value / bs.TotalAssets.Value) * 100;

        double? prevValue = null;
        double? yoyChange = null;
        if (prevIncome?.NetProfit != null && bs.TotalAssets != null)
        {
            // 简化：用当前总资产近似上年总资产（精确计算需上年资产负债表）
            prevValue = (double)(prevIncome.NetProfit.Value / bs.TotalAssets.Value) * 100;
            yoyChange = value - prevValue;
        }

        // ROA评分阈值参照ROE但更严格（ROA通常低于ROE）
        var roaThresholds = new (double, double)[]
        {
            (0, 0), (3, 40), (5, 60), (10, 80), (15, 100)
        };
        double score = ScoreEngine.LinearInterpolate(value, roaThresholds);

        return new MetricItemDto
        {
            Value = Math.Round(value, 2),
            PrevYear = prevValue.HasValue ? Math.Round(prevValue.Value, 2) : null,
            YoyChange = yoyChange.HasValue ? Math.Round(yoyChange.Value, 2) : null,
            Trend = ScoreEngine.GetTrend(yoyChange),
            Score = Math.Round(score, 1),
            Level = ScoreEngine.GetLevel(score),
            Label = "ROA",
            Unit = "%",
            Description = "总资产收益率 = 净利润 / 总资产"
        };
    }

    /// <summary>毛利率 = 毛利润 / 营业收入 × 100%</summary>
    private MetricItemDto? CalcGrossMargin(StockIncome? income, StockIncome? prevIncome)
    {
        if (income?.Revenue == null || income.Revenue == 0)
            return null;

        // 毛利润优先取表中的 GrossProfit，若为空则用 Revenue - OperCost 计算
        decimal grossProfit = income.GrossProfit
            ?? (income.Revenue.Value - (income.OperCost ?? 0));

        double value = (double)(grossProfit / income.Revenue.Value) * 100;

        double? prevValue = null;
        double? yoyChange = null;
        if (prevIncome?.Revenue != null && prevIncome.Revenue != 0)
        {
            decimal prevGp = prevIncome.GrossProfit
                ?? (prevIncome.Revenue.Value - (prevIncome.OperCost ?? 0));
            prevValue = (double)(prevGp / prevIncome.Revenue.Value) * 100;
            yoyChange = value - prevValue;
        }

        double score = ScoreEngine.LinearInterpolate(value, ScoreEngine.GrossMarginThresholds);

        return new MetricItemDto
        {
            Value = Math.Round(value, 2),
            PrevYear = prevValue.HasValue ? Math.Round(prevValue.Value, 2) : null,
            YoyChange = yoyChange.HasValue ? Math.Round(yoyChange.Value, 2) : null,
            Trend = ScoreEngine.GetTrend(yoyChange),
            Score = Math.Round(score, 1),
            Level = ScoreEngine.GetLevel(score),
            Label = "毛利率",
            Unit = "%",
            Description = "毛利率 = 毛利润 / 营业收入"
        };
    }

    /// <summary>净利率 = 净利润 / 营业收入 × 100%</summary>
    private MetricItemDto? CalcNetMargin(StockIncome? income, StockIncome? prevIncome)
    {
        if (income?.Revenue == null || income.Revenue == 0 || income.NetProfit == null)
            return null;

        double value = (double)(income.NetProfit.Value / income.Revenue.Value) * 100;

        double? prevValue = null;
        double? yoyChange = null;
        if (prevIncome?.Revenue != null && prevIncome.Revenue != 0 && prevIncome.NetProfit != null)
        {
            prevValue = (double)(prevIncome.NetProfit.Value / prevIncome.Revenue.Value) * 100;
            yoyChange = value - prevValue;
        }

        var netMarginThresholds = new (double, double)[]
        {
            (0, 20), (5, 50), (10, 70), (20, 90), (30, 100)
        };
        double score = ScoreEngine.LinearInterpolate(value, netMarginThresholds);

        return new MetricItemDto
        {
            Value = Math.Round(value, 2),
            PrevYear = prevValue.HasValue ? Math.Round(prevValue.Value, 2) : null,
            YoyChange = yoyChange.HasValue ? Math.Round(yoyChange.Value, 2) : null,
            Trend = ScoreEngine.GetTrend(yoyChange),
            Score = Math.Round(score, 1),
            Level = ScoreEngine.GetLevel(score),
            Label = "净利率",
            Unit = "%",
            Description = "净利率 = 净利润 / 营业收入"
        };
    }

    /// <summary>资产负债率 = 总负债 / 总资产 × 100%（金融行业豁免，返回满分100）</summary>
    private MetricItemDto? CalcDebtRatio(StockBalanceSheet? bs, string? industry)
    {
        if (bs?.TotalAssets == null || bs.TotalAssets == 0 || bs.TotalLiab == null)
            return null;

        double value = (double)(bs.TotalLiab.Value / bs.TotalAssets.Value) * 100;

        // 金融行业负债率豁免：银行/保险/证券的高负债率是正常经营模式，返回满分
        bool isFinance = ScoreEngine.IsFinanceIndustry(industry);
        double score = isFinance ? 100 : ScoreEngine.LinearInterpolate(value, ScoreEngine.DebtRatioThresholds);

        return new MetricItemDto
        {
            Value = Math.Round(value, 2),
            Score = Math.Round(score, 1),
            Level = ScoreEngine.GetLevel(score),
            Label = "资产负债率",
            Unit = "%",
            Description = "资产负债率 = 总负债 / 总资产（金融行业豁免评分）"
        };
    }

    /// <summary>营收同比增长率 = (本期营收 - 去年同期营收) / 去年同期营收 × 100%</summary>
    private MetricItemDto? CalcRevenueGrowth(StockIncome? income, StockIncome? prevIncome)
    {
        if (income?.Revenue == null || prevIncome?.Revenue == null || prevIncome.Revenue == 0)
            return null;

        double value = (double)((income.Revenue.Value - prevIncome.Revenue.Value) / prevIncome.Revenue.Value) * 100;

        double score = ScoreEngine.LinearInterpolate(value, ScoreEngine.RevenueGrowthThresholds);

        return new MetricItemDto
        {
            Value = Math.Round(value, 2),
            Score = Math.Round(score, 1),
            Level = ScoreEngine.GetLevel(score),
            Label = "营收增长率",
            Unit = "%",
            Description = "营收同比增长率"
        };
    }

    /// <summary>净利同比增长率</summary>
    private MetricItemDto? CalcProfitGrowth(StockIncome? income, StockIncome? prevIncome)
    {
        if (income?.NetProfit == null || prevIncome?.NetProfit == null || prevIncome.NetProfit == 0)
            return null;

        double value = (double)((income.NetProfit.Value - prevIncome.NetProfit.Value) / Math.Abs(prevIncome.NetProfit.Value)) * 100;

        double score = ScoreEngine.LinearInterpolate(value, ScoreEngine.ProfitGrowthThresholds);

        return new MetricItemDto
        {
            Value = Math.Round(value, 2),
            Score = Math.Round(score, 1),
            Level = ScoreEngine.GetLevel(score),
            Label = "净利增长率",
            Unit = "%",
            Description = "净利润同比增长率"
        };
    }

    /// <summary>现金流质量 = 经营现金流 / 净利润</summary>
    private MetricItemDto? CalcOcfToProfit(StockCashflow? cf, StockIncome? income)
    {
        if (cf?.OperCashFlow == null || income?.NetProfit == null || income.NetProfit == 0)
            return null;

        double value = (double)(cf.OperCashFlow.Value / income.NetProfit.Value);

        double score = ScoreEngine.LinearInterpolate(value, ScoreEngine.OcfToProfitThresholds);

        return new MetricItemDto
        {
            Value = Math.Round(value, 2),
            Score = Math.Round(score, 1),
            Level = ScoreEngine.GetLevel(score),
            Label = "现金流质量",
            Unit = "倍",
            Description = "经营现金流 / 净利润，衡量盈利质量"
        };
    }

    /// <summary>自由现金流 FCF = 经营现金流 - CapEx</summary>
    private MetricItemDto? CalcFreeCashFlow(StockCashflow? cf)
    {
        if (cf?.OperCashFlow == null)
            return null;

        decimal capEx = cf.CapEx ?? 0;
        decimal fcf = cf.OperCashFlow.Value - capEx;
        double value = (double)fcf / 1e8; // 转亿元

        // FCF 评分：正值越高越好
        var fcfThresholds = new (double, double)[]
        {
            (-10, 20), (0, 50), (10, 70), (50, 90), (100, 100)
        };
        double score = ScoreEngine.LinearInterpolate(value, fcfThresholds);

        return new MetricItemDto
        {
            Value = Math.Round(value, 2),
            Score = Math.Round(score, 1),
            Level = ScoreEngine.GetLevel(score),
            Label = "自由现金流",
            Unit = "亿元",
            Description = "FCF = 经营现金流 - 资本支出(CapEx)"
        };
    }

    /// <summary>商誉占比 = 商誉 / 总资产 × 100%</summary>
    private MetricItemDto? CalcGoodwillRatio(StockBalanceSheet? bs)
    {
        if (bs?.Goodwill == null || bs.TotalAssets == null || bs.TotalAssets == 0)
            return null;

        double value = (double)(bs.Goodwill.Value / bs.TotalAssets.Value) * 100;

        double score = ScoreEngine.LinearInterpolate(value, ScoreEngine.GoodwillRatioThresholds);

        return new MetricItemDto
        {
            Value = Math.Round(value, 2),
            Score = Math.Round(score, 1),
            Level = ScoreEngine.GetLevel(score),
            Label = "商誉占比",
            Unit = "%",
            Description = "商誉 / 总资产"
        };
    }

    /// <summary>应收占比 = 应收账款 / 营业收入 × 100%</summary>
    private MetricItemDto? CalcRecvRatio(StockBalanceSheet? bs, StockIncome? income)
    {
        if (bs?.AccountRecv == null || income?.Revenue == null || income.Revenue == 0)
            return null;

        double value = (double)(bs.AccountRecv.Value / income.Revenue.Value) * 100;

        double score = ScoreEngine.LinearInterpolate(value, ScoreEngine.RecvRatioThresholds);

        return new MetricItemDto
        {
            Value = Math.Round(value, 2),
            Score = Math.Round(score, 1),
            Level = ScoreEngine.GetLevel(score),
            Label = "应收占比",
            Unit = "%",
            Description = "应收账款 / 营业收入"
        };
    }
}
