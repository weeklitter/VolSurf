using Microsoft.Extensions.Options;
using VolSurf.Core.BlackScholes;
using VolSurf.Core.Models;
using VolSurf.Core.Options;
using VolSurf.Data.Entities;

namespace VolSurf.Core.Services;

/// <summary>
/// IV / Greeks 批量计算服务
///
/// 输入：期权日线 + 合约信息 + 标的价格
/// 流程：
///   1. 过滤无成交/即将到期
///   2. 调用 IvSolver 反推 IV
///   3. 计算 Greeks（BsPricer 返回年化原值，此处做 /365（Theta）、/100（Vega/Rho）转换）
///   4. 标记低置信度/异常
/// </summary>
public class IvCalculationService
{
    private readonly double _riskFreeRate;

    public IvCalculationService(IOptions<RiskFreeRateOptions> rateOptions)
    {
        _riskFreeRate = rateOptions.Value.DefaultRate;
    }

    public IvResult Calculate(OptionDaily daily, OptionContract contract, double underlyingPrice)
    {
        // 1. 过滤：无成交
        if (daily.Vol == 0 || daily.Settle == null || daily.Settle <= 0)
            return IvResult.Empty("无成交");

        // 2. 过滤：到期时间不足
        double T = (contract.MaturityDate.Date - daily.TradeDate.Date).TotalDays / 365.0;
        if (T <= 0.001)
            return IvResult.Empty("即将到期");

        // 3. 计算 IV
        double marketPrice = (double)daily.Settle.Value;
        var type = contract.CallPut == "C" ? OptionType.Call : OptionType.Put;
        double K = (double)contract.ExercisePrice;

        double? iv = IvSolver.Solve(marketPrice, type, underlyingPrice, K, T, _riskFreeRate);

        if (iv == null)
            return IvResult.Empty("不收敛");

        // 4. IV 合理性检查
        if (iv < 0.05 || iv > 2.0)
            return IvResult.AnomalyValue(iv.Value, "IV超出合理范围");

        // 5. 计算 Greeks（BsPricer 返回年化原值，在此做转换）
        double delta = BsPricer.Delta(type, underlyingPrice, K, T, _riskFreeRate, iv.Value);
        double gamma = BsPricer.Gamma(underlyingPrice, K, T, _riskFreeRate, iv.Value);
        double thetaAnnual = BsPricer.Theta(type, underlyingPrice, K, T, _riskFreeRate, iv.Value);
        double vegaRaw = BsPricer.Vega(underlyingPrice, K, T, _riskFreeRate, iv.Value);
        double rhoRaw = BsPricer.Rho(type, underlyingPrice, K, T, _riskFreeRate, iv.Value);

        // 转换：Theta /365 转每日，Vega /100 转每 1%，Rho /100 转每 1%
        double theta = thetaAnnual / 365.0;
        double vega = vegaRaw / 100.0;
        double rho = rhoRaw / 100.0;

        // 6. 置信度标记：深度实值/虚值/低流动性
        bool lowConfidence = Math.Abs(delta) < 0.02 || Math.Abs(delta) > 0.98;

        return new IvResult
        {
            Iv = iv.Value,
            Delta = delta,
            Gamma = gamma,
            Theta = theta,
            Vega = vega,
            Rho = rho,
            Confidence = !lowConfidence,
            Anomaly = false
        };
    }
}