using VolSurf.Core.Models.Dto;
using VolSurf.Data.Entities;
using VolSurf.Data.Repositories;

namespace VolSurf.Core.Services;

/// <summary>
/// 股票异常预警服务。
///
/// 规则引擎自动检测13条预警规则，输出结构化列表。
/// 每条规则包含：判断条件、消息模板、金融行业豁免逻辑。
///
/// 金融行业（银行/保险/证券）豁免负债率相关规则。
/// </summary>
public class WarningService(IStockRepository repo)
{
    /// <summary>获取某股票的全部预警</summary>
    public async Task<List<WarningDto>> GetWarningsAsync(string tsCode, string? industry)
    {
        var warnings = new List<WarningDto>();
        bool isFinance = ScoreEngine.IsFinanceIndustry(industry);

        // 1. 拉取数据
        var incomes = await repo.GetIncomeStatementsAsync(tsCode, 8);
        var balanceSheets = await repo.GetBalanceSheetsAsync(tsCode, 8);
        var cashflows = await repo.GetCashflowStatementsAsync(tsCode, 8);
        var latestDailyBasic = await repo.GetLatestDailyBasicAsync(tsCode);
        var dailyBasicHistory = await repo.GetDailyBasicHistoryAsync(tsCode, 1250);

        var latestIncome = incomes.LastOrDefault();
        var latestBs = balanceSheets.LastOrDefault();
        var latestCf = cashflows.LastOrDefault();

        // 去年同期数据（用于同比计算）
        var prevYearIncome = incomes.Count >= 5 ? incomes[^5] : null;
        var prevYearBs = balanceSheets.Count >= 5 ? balanceSheets[^5] : null;

        // ═══════════════════════════════════════════════════════════════════════════
        // 规则1：商誉过高（商誉/总资产 > 30%）- warn
        // ═══════════════════════════════════════════════════════════════════════════
        if (latestBs?.Goodwill != null && latestBs.TotalAssets != null && latestBs.TotalAssets != 0)
        {
            double ratio = (double)(latestBs.Goodwill.Value / latestBs.TotalAssets.Value) * 100;
            if (ratio > 30 && ratio <= 50)
            {
                warnings.Add(new WarningDto
                {
                    Type = "goodwill_high",
                    Level = "warn",
                    Message = $"商誉占比{ratio:F1}%，高于30%警戒线",
                    Value = Math.Round(ratio, 1),
                    Threshold = 30
                });
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // 规则2：商誉极高（商誉/总资产 > 50%）- danger
        // ═══════════════════════════════════════════════════════════════════════════
        if (latestBs?.Goodwill != null && latestBs.TotalAssets != null && latestBs.TotalAssets != 0)
        {
            double ratio = (double)(latestBs.Goodwill.Value / latestBs.TotalAssets.Value) * 100;
            if (ratio > 50)
            {
                warnings.Add(new WarningDto
                {
                    Type = "goodwill_extreme",
                    Level = "danger",
                    Message = $"商誉占比{ratio:F1}%，高于50%极度警戒线，存在大幅减值风险",
                    Value = Math.Round(ratio, 1),
                    Threshold = 50
                });
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // 规则3：应收激增（应收账款同比 > 50%）- warn
        // ═══════════════════════════════════════════════════════════════════════════
        if (latestBs?.AccountRecv != null && prevYearBs?.AccountRecv != null
            && prevYearBs.AccountRecv != 0)
        {
            double growth = (double)((latestBs.AccountRecv.Value - prevYearBs.AccountRecv.Value)
                / prevYearBs.AccountRecv.Value) * 100;
            if (growth > 50)
            {
                warnings.Add(new WarningDto
                {
                    Type = "recv_surge",
                    Level = "warn",
                    Message = $"应收账款同比增长{growth:F1}%，超过50%警戒线",
                    Value = Math.Round(growth, 1),
                    Threshold = 50
                });
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // 规则4：存货激增（存货同比 > 50%）- warn
        // ═══════════════════════════════════════════════════════════════════════════
        if (latestBs?.Inventory != null && prevYearBs?.Inventory != null
            && prevYearBs.Inventory != 0)
        {
            double growth = (double)((latestBs.Inventory.Value - prevYearBs.Inventory.Value)
                / prevYearBs.Inventory.Value) * 100;
            if (growth > 50)
            {
                warnings.Add(new WarningDto
                {
                    Type = "inventory_surge",
                    Level = "warn",
                    Message = $"存货同比增长{growth:F1}%，超过50%警戒线",
                    Value = Math.Round(growth, 1),
                    Threshold = 50
                });
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // 规则5：现金流恶化（经营现金流/净利润 < 0.5）- warn
        // ═══════════════════════════════════════════════════════════════════════════
        if (latestCf?.OperCashFlow != null && latestIncome?.NetProfit != null
            && latestIncome.NetProfit != 0)
        {
            double ratio = (double)(latestCf.OperCashFlow.Value / latestIncome.NetProfit.Value);
            if (ratio < 0.5 && ratio >= 0)
            {
                warnings.Add(new WarningDto
                {
                    Type = "ocf_low",
                    Level = "warn",
                    Message = $"经营现金流/净利润={ratio:F2}，低于0.5，盈利质量存疑",
                    Value = Math.Round(ratio, 2),
                    Threshold = 0.5
                });
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // 规则6：现金流为负（经营现金流 < 0）- danger
        // ═══════════════════════════════════════════════════════════════════════════
        if (latestCf?.OperCashFlow != null && latestCf.OperCashFlow < 0)
        {
            warnings.Add(new WarningDto
            {
                Type = "ocf_negative",
                Level = "danger",
                Message = $"经营现金流为负（{(double)latestCf.OperCashFlow.Value / 1e8:F2}亿元），造血能力不足",
                Value = Math.Round((double)latestCf.OperCashFlow.Value / 1e8, 2),
                Threshold = 0
            });
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // 规则7：ROE下滑（ROE同比下降 > 5个百分点）- warn
        // ═══════════════════════════════════════════════════════════════════════════
        if (latestIncome?.NetProfit != null && latestBs?.TotalEquity != null
            && latestBs.TotalEquity != 0
            && prevYearIncome?.NetProfit != null && prevYearBs?.TotalEquity != null
            && prevYearBs.TotalEquity != 0)
        {
            double currentRoe = (double)(latestIncome.NetProfit.Value / latestBs.TotalEquity.Value) * 100;
            double prevRoe = (double)(prevYearIncome.NetProfit.Value / prevYearBs.TotalEquity.Value) * 100;
            double decline = prevRoe - currentRoe;

            if (decline > 5)
            {
                warnings.Add(new WarningDto
                {
                    Type = "roe_decline",
                    Level = "warn",
                    Message = $"ROE同比下降{decline:F1}个百分点（{prevRoe:F1}% -> {currentRoe:F1}%）",
                    Value = Math.Round(decline, 1),
                    Threshold = 5
                });
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // 规则8：营收下滑（营收同比 < -10%）- warn
        // ═══════════════════════════════════════════════════════════════════════════
        if (latestIncome?.Revenue != null && prevYearIncome?.Revenue != null
            && prevYearIncome.Revenue != 0)
        {
            double growth = (double)((latestIncome.Revenue.Value - prevYearIncome.Revenue.Value)
                / prevYearIncome.Revenue.Value) * 100;
            if (growth < -10)
            {
                warnings.Add(new WarningDto
                {
                    Type = "revenue_decline",
                    Level = "warn",
                    Message = $"营收同比下降{Math.Abs(growth):F1}%",
                    Value = Math.Round(growth, 1),
                    Threshold = -10
                });
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // 规则9：净利润为负（净利润 < 0）- danger
        // ═══════════════════════════════════════════════════════════════════════════
        if (latestIncome?.NetProfit != null && latestIncome.NetProfit < 0)
        {
            warnings.Add(new WarningDto
            {
                Type = "net_loss",
                Level = "danger",
                Message = $"净利润为负（{(double)latestIncome.NetProfit.Value / 1e8:F2}亿元）",
                Value = Math.Round((double)latestIncome.NetProfit.Value / 1e8, 2),
                Threshold = 0
            });
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // 规则10：业绩变脸（单季度净利润同比由正转负）- warn
        // ═══════════════════════════════════════════════════════════════════════════
        if (latestIncome?.NetProfit != null && prevYearIncome?.NetProfit != null)
        {
            if (latestIncome.NetProfit < 0 && prevYearIncome.NetProfit > 0)
            {
                warnings.Add(new WarningDto
                {
                    Type = "profit_reversal",
                    Level = "warn",
                    Message = $"业绩变脸：去年同期盈利{(double)prevYearIncome.NetProfit.Value / 1e8:F2}亿元，本期亏损{(double)latestIncome.NetProfit.Value / 1e8:F2}亿元",
                    Value = Math.Round((double)latestIncome.NetProfit.Value / 1e8, 2),
                    Threshold = 0
                });
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // 规则11：负债率过高（资产负债率 > 70%，非金融行业）- warn
        // 金融行业豁免：银行/保险/证券高负债率是正常经营模式
        // ═══════════════════════════════════════════════════════════════════════════
        if (!isFinance && latestBs?.TotalLiab != null && latestBs.TotalAssets != null
            && latestBs.TotalAssets != 0)
        {
            double ratio = (double)(latestBs.TotalLiab.Value / latestBs.TotalAssets.Value) * 100;
            if (ratio > 70 && ratio <= 80)
            {
                warnings.Add(new WarningDto
                {
                    Type = "debt_high",
                    Level = "warn",
                    Message = $"资产负债率{ratio:F1}%，高于70%警戒线" +
                        (isFinance ? "（金融行业豁免）" : ""),
                    Value = Math.Round(ratio, 1),
                    Threshold = 70
                });
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // 规则12：负债率极高（资产负债率 > 80%，非金融行业）- danger
        // 金融行业豁免
        // ═══════════════════════════════════════════════════════════════════════════
        if (!isFinance && latestBs?.TotalLiab != null && latestBs.TotalAssets != null
            && latestBs.TotalAssets != 0)
        {
            double ratio = (double)(latestBs.TotalLiab.Value / latestBs.TotalAssets.Value) * 100;
            if (ratio > 80)
            {
                warnings.Add(new WarningDto
                {
                    Type = "debt_extreme",
                    Level = "danger",
                    Message = $"资产负债率{ratio:F1}%，高于80%极度警戒线",
                    Value = Math.Round(ratio, 1),
                    Threshold = 80
                });
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // 规则13a：估值极高（PE 5年分位 > 90%）- warn
        // ═══════════════════════════════════════════════════════════════════════════
        if (latestDailyBasic?.PeTtm != null && latestDailyBasic.PeTtm > 0)
        {
            var peHistory = dailyBasicHistory
                .Where(h => h.PeTtm.HasValue && h.PeTtm > 0)
                .Select(h => (double)h.PeTtm!.Value)
                .ToList();

            if (peHistory.Count > 60) // 至少60天数据才有意义
            {
                double currentPe = (double)latestDailyBasic.PeTtm.Value;
                double percentile = (double)peHistory.Count(v => v <= currentPe) / peHistory.Count * 100;

                if (percentile > 90)
                {
                    warnings.Add(new WarningDto
                    {
                        Type = "valuation_high",
                        Level = "warn",
                        Message = $"PE(TTM)处于近5年{percentile:F0}%分位，估值偏高",
                        Value = Math.Round(percentile, 0),
                        Threshold = 90
                    });
                }

                // ═══════════════════════════════════════════════════════════════════════════
                // 规则13b：估值极低（PE 5年分位 < 10%）- info
                // ═══════════════════════════════════════════════════════════════════════════
                if (percentile < 10)
                {
                    warnings.Add(new WarningDto
                    {
                        Type = "valuation_low",
                        Level = "info",
                        Message = $"PE(TTM)处于近5年{percentile:F0}%分位，估值偏低（可能存在投资机会，也可能基本面恶化）",
                        Value = Math.Round(percentile, 0),
                        Threshold = 10
                    });
                }
            }
        }

        return warnings;
    }
}
