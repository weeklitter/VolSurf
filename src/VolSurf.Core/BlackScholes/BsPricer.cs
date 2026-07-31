using MathNet.Numerics.Distributions;

namespace VolSurf.Core.BlackScholes;

/// <summary>
/// Black-Scholes 欧式期权定价（含 Merton 股息率扩展）
///
/// 数学公式：
///   d1 = (ln(S/K) + (r - q + σ²/2) * T) / (σ * √T)
///   d2 = d1 - σ * √T
///   Call = S * e^(-qT) * N(d1) - K * e^(-rT) * N(d2)
///   Put  = K * e^(-rT) * N(-d2) - S * e^(-qT) * N(-d1)
///
/// Greeks（年化原值）：
///   Delta_call = e^(-qT) * N(d1)
///   Delta_put  = e^(-qT) * (N(d1) - 1)
///   Gamma      = e^(-qT) * N'(d1) / (S * σ * √T)
///   Vega       = S * e^(-qT) * N'(d1) * √T
///   Rho_call   = K * T * e^(-rT) * N(d2)
///   Rho_put    = -K * T * e^(-rT) * N(-d2)
///   Theta_call = -(S * e^(-qT) * N'(d1) * σ) / (2*√T) - r*K*e^(-rT)*N(d2) + q*S*e^(-qT)*N(d1)
///   Theta_put  = -(S * e^(-qT) * N'(d1) * σ) / (2*√T) + r*K*e^(-rT)*N(-d2) - q*S*e^(-qT)*N(-d1)
///
/// MVP 默认 q=0。调用方按需做 /365（Theta）、/100（Vega、Rho）转换。
/// </summary>
public class BsPricer
{
    private static readonly Normal StandardNormal = new(0, 1);

    /// <summary>欧式期权理论价格（含股息率 q）</summary>
    public static double Price(
        OptionType type,
        double S, double K, double T, double r, double sigma,
        double q = 0)
    {
        if (T <= 0)
            return type == OptionType.Call
                ? Math.Max(0, S * Math.Exp(-q * T) - K * Math.Exp(-r * T))
                : Math.Max(0, K * Math.Exp(-r * T) - S * Math.Exp(-q * T));

        var (d1, d2) = CalcD(S, K, T, r, sigma, q);
        double discountS = S * Math.Exp(-q * T);
        double discountK = K * Math.Exp(-r * T);

        return type == OptionType.Call
            ? discountS * NormCdf(d1) - discountK * NormCdf(d2)
            : discountK * NormCdf(-d2) - discountS * NormCdf(-d1);
    }

    /// <summary>Delta（年化原值）</summary>
    public static double Delta(OptionType type, double S, double K, double T, double r, double sigma, double q = 0)
    {
        if (T <= 0) return type == OptionType.Call ? 1.0 : -1.0;
        var (d1, _) = CalcD(S, K, T, r, sigma, q);
        double discountS = Math.Exp(-q * T);
        return type == OptionType.Call
            ? discountS * NormCdf(d1)
            : discountS * (NormCdf(d1) - 1);
    }

    /// <summary>Gamma</summary>
    public static double Gamma(double S, double K, double T, double r, double sigma, double q = 0)
    {
        if (T <= 0) return 0;
        var (d1, _) = CalcD(S, K, T, r, sigma, q);
        return Math.Exp(-q * T) * NormPdf(d1) / (S * sigma * Math.Sqrt(T));
    }

    /// <summary>Theta（年化原值，调用方按需 /365 转为每日）</summary>
    public static double Theta(OptionType type, double S, double K, double T, double r, double sigma, double q = 0)
    {
        if (T <= 0) return 0;
        var (d1, d2) = CalcD(S, K, T, r, sigma, q);
        double discountS = S * Math.Exp(-q * T);
        double discountK = K * Math.Exp(-r * T);
        double common = -(discountS * NormPdf(d1) * sigma) / (2 * Math.Sqrt(T));

        if (type == OptionType.Call)
            return common - r * discountK * NormCdf(d2) + q * discountS * NormCdf(d1);
        else
            return common + r * discountK * NormCdf(-d2) - q * discountS * NormCdf(-d1);
    }

    /// <summary>Vega（原值，调用方按需 /100 转为每1%波动率）</summary>
    public static double Vega(double S, double K, double T, double r, double sigma, double q = 0)
    {
        if (T <= 0) return 0;
        var (d1, _) = CalcD(S, K, T, r, sigma, q);
        return S * Math.Exp(-q * T) * NormPdf(d1) * Math.Sqrt(T);
    }

    /// <summary>Rho（原值，调用方按需 /100 转为每1%利率）</summary>
    public static double Rho(OptionType type, double S, double K, double T, double r, double sigma, double q = 0)
    {
        if (T <= 0) return 0;
        var (_, d2) = CalcD(S, K, T, r, sigma, q);
        double discountK = K * T * Math.Exp(-r * T);
        return type == OptionType.Call
            ? discountK * NormCdf(d2)
            : -discountK * NormCdf(-d2);
    }

    // ── 内部辅助函数 ──
    private static double NormCdf(double x) => StandardNormal.CumulativeDistribution(x);

    private static double NormPdf(double x)
        => Math.Exp(-x * x / 2) / Math.Sqrt(2 * Math.PI);

    private static (double d1, double d2) CalcD(double S, double K, double T, double r, double sigma, double q)
    {
        double d1 = (Math.Log(S / K) + (r - q + sigma * sigma / 2) * T) / (sigma * Math.Sqrt(T));
        double d2 = d1 - sigma * Math.Sqrt(T);
        return (d1, d2);
    }
}