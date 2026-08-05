namespace VolSurf.Core.Services;

/// <summary>
/// 线性插值评分引擎。
///
/// 评分原理：
///   每个指标定义一组 (threshold, score) 阈值点，按 threshold 排序。
///   给定 value，找到它所在的区间 [t_i, t_{i+1}]，
///   在该区间内线性插值：score = s_i + (s_{i+1} - s_i) * (value - t_i) / (t_{i+1} - t_i)
///   超出最大阈值 -> 取最高分；低于最小阈值 -> 取最低分。
///
/// 金融行业（银行/保险/证券）特殊处理：
///   资产负债率指标豁免（返回满分100），因为金融行业高负债率是正常经营模式。
/// </summary>
public class ScoreEngine
{
    // ── 金融行业关键词（用于判断是否豁免负债率评分）──
    private static readonly HashSet<string> FinanceIndustryKeywords = new()
    {
        "银行", "保险", "证券", "金融"
    };

    /// <summary>判断是否为金融行业</summary>
    public static bool IsFinanceIndustry(string? industry)
    {
        if (string.IsNullOrEmpty(industry)) return false;
        return FinanceIndustryKeywords.Any(k => industry.Contains(k));
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 核心插值方法
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 线性插值评分。
    ///
    /// thresholds 必须按 value 升序排列，例如：
    ///   越大越好的指标（如ROE）：
    ///     [(0, 0), (5, 40), (10, 60), (15, 80), (20, 100)]
    ///   越小越好的指标（如资产负债率）：
    ///     [(30, 100), (50, 80), (70, 50), (80, 20)]
    /// </summary>
    /// <param name="value">指标当前值</param>
    /// <param name="thresholds">阈值点数组，每项为 (阈值, 评分)</param>
    /// <returns>0-100 的评分</returns>
    public static double LinearInterpolate(double value, params (double Threshold, double Score)[] thresholds)
    {
        if (thresholds.Length == 0) return 50; // 无阈值定义时返回中等分

        // 按 Threshold 升序排序
        var sorted = thresholds.OrderBy(t => t.Threshold).ToArray();

        // 低于最小阈值 -> 取最低分
        if (value <= sorted[0].Threshold)
            return sorted[0].Score;

        // 高于最大阈值 -> 取最高分
        if (value >= sorted[^1].Threshold)
            return sorted[^1].Score;

        // 在区间内线性插值
        for (int i = 0; i < sorted.Length - 1; i++)
        {
            var (t1, s1) = sorted[i];
            var (t2, s2) = sorted[i + 1];

            if (value >= t1 && value <= t2)
            {
                double ratio = (value - t1) / (t2 - t1);
                return s1 + (s2 - s1) * ratio;
            }
        }

        return sorted[^1].Score; // 理论上不会走到这里
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 各指标评分阈值定义
    // ═══════════════════════════════════════════════════════════════════════════

    // ── 财务健康分（healthScore）各指标阈值 ──

    /// <summary>ROE 评分阈值：≥20%->100, 15%->80, 10%->60, 5%->40, ≤0%->0</summary>
    public static readonly (double, double)[] RoeThresholds =
    {
        (0, 0), (5, 40), (10, 60), (15, 80), (20, 100)
    };

    /// <summary>毛利率评分阈值：≥60%->100, 40%->80, 20%->60, ≤0%->20</summary>
    public static readonly (double, double)[] GrossMarginThresholds =
    {
        (0, 20), (20, 60), (40, 80), (60, 100)
    };

    /// <summary>资产负债率评分阈值：≤30%->100, 50%->80, 70%->50, ≥80%->20（金融行业豁免）</summary>
    public static readonly (double, double)[] DebtRatioThresholds =
    {
        (30, 100), (50, 80), (70, 50), (80, 20)
    };

    /// <summary>现金流质量(OCF/利润)评分阈值：≥1.2->100, 0.8->80, 0.5->50, ≤0->20</summary>
    public static readonly (double, double)[] OcfToProfitThresholds =
    {
        (0, 20), (0.5, 50), (0.8, 80), (1.2, 100)
    };

    /// <summary>商誉占比评分阈值：≤5%->100, 20%->80, 30%->50, ≥50%->20</summary>
    public static readonly (double, double)[] GoodwillRatioThresholds =
    {
        (5, 100), (20, 80), (30, 50), (50, 20)
    };

    /// <summary>应收占比评分阈值：≤10%->100, 30%->80, 50%->50, ≥80%->20</summary>
    public static readonly (double, double)[] RecvRatioThresholds =
    {
        (10, 100), (30, 80), (50, 50), (80, 20)
    };

    // ── 成长分（growthScore）各指标阈值 ──

    /// <summary>营收增长率评分阈值：≥30%->100, 15%->80, 5%->60, 0%->40, ≤-10%->0</summary>
    public static readonly (double, double)[] RevenueGrowthThresholds =
    {
        (-10, 0), (0, 40), (5, 60), (15, 80), (30, 100)
    };

    /// <summary>净利增长率评分阈值：同营收增长率</summary>
    public static readonly (double, double)[] ProfitGrowthThresholds =
    {
        (-10, 0), (0, 40), (5, 60), (15, 80), (30, 100)
    };

    // ── 估值分（valueScore）各指标阈值 ──

    /// <summary>PE分位评分阈值：≤20%->100, 40%->80, 60%->60, 80%->40, ≥90%->20</summary>
    public static readonly (double, double)[] PePercentileThresholds =
    {
        (20, 100), (40, 80), (60, 60), (80, 40), (90, 20)
    };

    /// <summary>PB分位评分阈值：同PE分位</summary>
    public static readonly (double, double)[] PbPercentileThresholds =
    {
        (20, 100), (40, 80), (60, 60), (80, 40), (90, 20)
    };

    /// <summary>股息率评分阈值：≥4%->100, 2%->80, 1%->60, ≤0%->20</summary>
    public static readonly (double, double)[] DvRatioThresholds =
    {
        (0, 20), (1, 60), (2, 80), (4, 100)
    };

    // ═══════════════════════════════════════════════════════════════════════════
    // 综合评分计算
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 计算财务健康分（满分100）。
    ///
    /// 权重分配：
    ///   ROE 25% | 毛利率 15% | 资产负债率 15% | 现金流质量 20% | 商誉占比 10% | 应收占比 15%
    ///
    /// 金融行业特殊处理：资产负债率豁免（按满分100计入），因为银行/保险/证券的高负债率是正常经营模式。
    /// </summary>
    public static double CalculateHealthScore(
        double? roe, double? grossMargin, double? debtRatio,
        double? ocfToProfit, double? goodwillRatio, double? recvRatio,
        string? industry)
    {
        bool isFinance = IsFinanceIndustry(industry);

        // ROE 25%
        double roeScore = roe.HasValue ? LinearInterpolate(roe.Value, RoeThresholds) : 50;

        // 毛利率 15%
        double gmScore = grossMargin.HasValue ? LinearInterpolate(grossMargin.Value, GrossMarginThresholds) : 50;

        // 资产负债率 15%（金融行业豁免 -> 满分100）
        double debtScore = isFinance
            ? 100
            : (debtRatio.HasValue ? LinearInterpolate(debtRatio.Value, DebtRatioThresholds) : 50);

        // 现金流质量 20%
        double ocfScore = ocfToProfit.HasValue ? LinearInterpolate(ocfToProfit.Value, OcfToProfitThresholds) : 50;

        // 商誉占比 10%
        double gwScore = goodwillRatio.HasValue ? LinearInterpolate(goodwillRatio.Value, GoodwillRatioThresholds) : 50;

        // 应收占比 15%
        double recvScore = recvRatio.HasValue ? LinearInterpolate(recvRatio.Value, RecvRatioThresholds) : 50;

        // 加权求和
        return roeScore * 0.25 + gmScore * 0.15 + debtScore * 0.15
             + ocfScore * 0.20 + gwScore * 0.10 + recvScore * 0.15;
    }

    /// <summary>
    /// 计算成长分（满分100）。
    ///
    /// 权重分配：营收增长率 40% | 净利增长率 40% | ROE趋势 20%
    /// ROE趋势：连续上升->100, 稳定(波动<2pp)->70, 下滑->40
    /// </summary>
    public static double CalculateGrowthScore(
        double? revenueGrowth, double? profitGrowth, double roeTrendScore)
    {
        // 营收增长率 40%
        double revScore = revenueGrowth.HasValue
            ? LinearInterpolate(revenueGrowth.Value, RevenueGrowthThresholds) : 50;

        // 净利增长率 40%
        double profScore = profitGrowth.HasValue
            ? LinearInterpolate(profitGrowth.Value, ProfitGrowthThresholds) : 50;

        // ROE趋势 20%（已由调用方计算好趋势分）
        return revScore * 0.40 + profScore * 0.40 + roeTrendScore * 0.20;
    }

    /// <summary>
    /// 计算估值分（满分100）。
    ///
    /// 权重分配：PE分位 45% | PB分位 35% | 股息率 20%
    /// </summary>
    public static double CalculateValueScore(
        double? pePercentile, double? pbPercentile, double? dvRatio)
    {
        // PE分位 45%
        double peScore = pePercentile.HasValue
            ? LinearInterpolate(pePercentile.Value, PePercentileThresholds) : 50;

        // PB分位 35%
        double pbScore = pbPercentile.HasValue
            ? LinearInterpolate(pbPercentile.Value, PbPercentileThresholds) : 50;

        // 股息率 20%
        double dvScore = dvRatio.HasValue
            ? LinearInterpolate(dvRatio.Value, DvRatioThresholds) : 50;

        return peScore * 0.45 + pbScore * 0.35 + dvScore * 0.20;
    }

    /// <summary>
    /// 计算综合评分（满分100）。
    ///
    /// 权重分配：财务健康分 40% | 成长分 30% | 估值分 30%
    /// </summary>
    public static double CalculateOverallScore(double healthScore, double growthScore, double valueScore)
    {
        return healthScore * 0.40 + growthScore * 0.30 + valueScore * 0.30;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 评分等级映射
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>根据评分(0-100)返回等级</summary>
    public static string GetLevel(double score) => score switch
    {
        >= 80 => "excellent",
        >= 65 => "good",
        >= 50 => "normal",
        >= 35 => "warn",
        _ => "danger"
    };

    /// <summary>根据趋势方向返回描述</summary>
    public static string GetTrend(double? yoyChange) => yoyChange switch
    {
        null => "stable",
        > 5 => "up",
        < -5 => "down",
        _ => "stable"
    };

    /// <summary>
    /// 计算ROE趋势评分。
    /// 输入近8季度ROE序列，判断趋势方向。
    /// 连续上升->100, 稳定(波动<2pp)->70, 下滑->40
    /// </summary>
    public static double CalculateRoeTrendScore(List<double> roeSeries)
    {
        if (roeSeries == null || roeSeries.Count < 2) return 70;

        // 简单线性回归斜率
        int n = roeSeries.Count;
        double xMean = (n - 1) / 2.0;
        double yMean = roeSeries.Average();
        double numerator = 0, denominator = 0;
        for (int i = 0; i < n; i++)
        {
            numerator += (i - xMean) * (roeSeries[i] - yMean);
            denominator += (i - xMean) * (i - xMean);
        }
        double slope = denominator == 0 ? 0 : numerator / denominator;

        // 斜率 > 0.5（每季度ROE上升>0.5个百分点）-> 上升趋势
        // 斜率 < -0.5 -> 下滑趋势
        // 否则 -> 稳定
        if (slope > 0.5) return 100;
        if (slope < -0.5) return 40;
        return 70;
    }
}
