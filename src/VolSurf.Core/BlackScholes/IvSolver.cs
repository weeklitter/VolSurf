namespace VolSurf.Core.BlackScholes;

/// <summary>
/// IV 反推求解器（bisection + Newton-Raphson 混合）
///
/// 求解思路：
/// 1. 多个边界检查（市场价/到期/内在价值/区间端点）
/// 2. bisection [SigmaMin, SigmaMax] 缩窄到 hi-lo &lt; 0.01 或达到 50 次迭代
/// 3. Newton-Raphson 从 bisection 中点开始，用 Vega 做导数
///    - 每次 Newton 步后检查：新 sigma 是否在 [lo, hi] 内
///    - 跳出区间则回退到 bisection 继续缩窄
///    - Newton 最大 50 次迭代
/// 4. Newton 不收敛则用 bisection 最后中点（精度 0.01）
/// 5. 最终检查 |price - marketPrice| &lt; max(0.0001, 0.001 * marketPrice)
/// </summary>
public class IvSolver
{
    private const double SigmaMin = 0.001;
    private const double SigmaMax = 5.0;
    private const double BisectTolerance = 0.01;
    private const int MaxBisectIter = 50;
    private const int MaxNewtonIter = 50;

    /// <summary>
    /// 从市场价格反推隐含波动率（bisection + Newton 混合）
    /// </summary>
    /// <param name="marketPrice">期权市场结算价</param>
    /// <param name="type">认购/认沽</param>
    /// <param name="S">标的价格</param>
    /// <param name="K">行权价</param>
    /// <param name="T">年化到期时间（自然日/365）</param>
    /// <param name="r">无风险利率</param>
    /// <param name="q">股息率（默认0）</param>
    /// <returns>IV 值，不收敛/异常返回 null</returns>
    public static double? Solve(
        double marketPrice,
        OptionType type,
        double S,
        double K,
        double T,
        double r,
        double q = 0)
    {
        // ── 边界检查 1：市场价格必须为正 ──
        if (marketPrice <= 0) return null;

        // ── 边界检查 2：T &lt;= 0 防御 ──
        if (T <= 0) return null;

        // ── 边界检查 3：内在价值校验 ──
        double intrinsic = type == OptionType.Call
            ? Math.Max(0, S * Math.Exp(-q * T) - K * Math.Exp(-r * T))
            : Math.Max(0, K * Math.Exp(-r * T) - S * Math.Exp(-q * T));
        if (marketPrice < intrinsic - 0.0001)
        {
            // 市场价格低于内在价值，数据异常
            return null;
        }

        // ── 边界检查 4：bisection 区间端点异号检查 ──
        double priceLo = BsPricer.Price(type, S, K, T, r, SigmaMin, q);
        double priceHi = BsPricer.Price(type, S, K, T, r, SigmaMax, q);

        if (marketPrice < priceLo || marketPrice > priceHi)
        {
            // 市场价格超出 B-S 可解释范围
            return null;
        }

        // ── bisection：缩窄到 [lo, hi]，hi-lo &lt; 0.01 或达到 50 次迭代 ──
        double lo = SigmaMin, hi = SigmaMax;
        for (int i = 0; i < MaxBisectIter && (hi - lo) >= BisectTolerance; i++)
        {
            double mid = (lo + hi) / 2;
            double midPrice = BsPricer.Price(type, S, K, T, r, mid, q);
            if (midPrice < marketPrice)
                lo = mid;
            else
                hi = mid;
        }

        // ── Newton-Raphson：从 bisection 中点开始，用 Vega 做导数 ──
        double sigma = (lo + hi) / 2;
        double tolerance = Math.Max(0.0001, 0.001 * marketPrice);

        for (int i = 0; i < MaxNewtonIter; i++)
        {
            double price = BsPricer.Price(type, S, K, T, r, sigma, q);
            double diff = price - marketPrice;

            if (Math.Abs(diff) < tolerance)
                return sigma;  // 收敛

            double vega = BsPricer.Vega(S, K, T, r, sigma, q);
            if (vega < 1e-10)
                break;  // Vega 太小，Newton 无法进行

            double nextSigma = sigma - diff / vega;

            // 如果 Newton 跳出 bisection 区间，回退到 bisection 继续缩窄
            if (nextSigma < lo || nextSigma > hi)
            {
                if (diff > 0)
                    hi = sigma;
                else
                    lo = sigma;
                sigma = (lo + hi) / 2;
            }
            else
            {
                sigma = nextSigma;
                // 更新区间边界
                if (diff > 0)
                    hi = sigma;
                else
                    lo = sigma;
            }
        }

        // ── Newton 不收敛，用 bisection 的最后中点作为近似值（精度 0.01） ──
        double finalMid = (lo + hi) / 2;
        double finalPrice = BsPricer.Price(type, S, K, T, r, finalMid, q);
        if (Math.Abs(finalPrice - marketPrice) < tolerance)
            return finalMid;

        return null;  // 最终检查未通过
    }
}