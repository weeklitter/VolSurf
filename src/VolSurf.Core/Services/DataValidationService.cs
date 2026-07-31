using VolSurf.Core.BlackScholes;
using VolSurf.Core.Models;
using VolSurf.Data.Entities;

namespace VolSurf.Core.Services;

/// <summary>
/// 数据校验服务：
/// 1. Put-Call Parity 校验（价格层面）：用各自 IV 计算理论价格，检查 C(IV_call) - P(IV_put) ≈ S - K*e^(-rT)
/// 2. 价格偏离校验：当日 settle 与前日 settle 偏离 > 20% 标记异常
/// 3. IV 范围检查
/// </summary>
public class DataValidationService
{
    /// <summary>
    /// Put-Call Parity 校验（价格层面）
    /// 用各自的 IV 分别计算理论价格，检查 C - P ≈ S - K*e^(-rT)
    /// 偏差 / 标的价 > 1% 标记异常
    /// </summary>
    public ValidationResult ValidateParity(
        List<OptionContract> contracts,
        List<IvGreeks> ivData,
        double underlyingPrice,
        double riskFreeRate,
        DateTime tradeDate)
    {
        var result = new ValidationResult { IsValid = true };

        // 按到期月+行权价分组，找同行权价同到期月的 Call/Put 对
        var pairs = contracts
            .Where(c => c.MaturityDate > tradeDate)
            .Join(ivData.Where(iv => iv.Iv.HasValue),
                  c => c.TsCode, iv => iv.TsCode,
                  (c, iv) => new { Contract = c, Iv = iv.Iv!.Value })
            .GroupBy(x => new { x.Contract.MaturityDate, x.Contract.ExercisePrice });

        foreach (var pair in pairs)
        {
            var call = pair.FirstOrDefault(x => x.Contract.CallPut == "C");
            var put = pair.FirstOrDefault(x => x.Contract.CallPut == "P");
            if (call == null || put == null) continue;

            double T = (pair.Key.MaturityDate.Date - tradeDate.Date).TotalDays / 365.0;
            if (T <= 0) continue;

            double K = (double)pair.Key.ExercisePrice;

            // 用各自的 IV 计算理论价格
            double callPrice = BsPricer.Price(OptionType.Call, underlyingPrice, K, T, riskFreeRate, call.Iv);
            double putPrice = BsPricer.Price(OptionType.Put, underlyingPrice, K, T, riskFreeRate, put.Iv);

            // Parity: C(IV_call) - P(IV_put) ≈ S - K*e^(-rT)
            double parityDiff = (callPrice - putPrice) - (underlyingPrice - K * Math.Exp(-riskFreeRate * T));
            double relativeDiff = Math.Abs(parityDiff) / underlyingPrice;

            if (relativeDiff > 0.01)  // 偏差 / S > 1%
            {
                result.Anomalies.Add(
                    $"Parity偏差: K={K}, 到期={pair.Key.MaturityDate:yyyy-MM-dd}, " +
                    $"偏差={parityDiff:F4} ({relativeDiff:P2})");
            }
        }

        if (result.Anomalies.Count > 0)
        {
            result.IsValid = false;
            result.Message = $"发现{result.Anomalies.Count}个Parity异常";
        }

        return result;
    }

    /// <summary>价格异常检测：当日 settle 与前日 settle 偏离 > 20% 标记异常</summary>
    public ValidationResult ValidatePriceDeviation(OptionDaily today, OptionDaily? yesterday)
    {
        var result = new ValidationResult { IsValid = true };

        if (yesterday?.Settle == null || today.Settle == null || yesterday.Settle == 0)
            return result;

        double deviation = Math.Abs((double)today.Settle - (double)yesterday.Settle) / (double)yesterday.Settle;
        if (deviation > 0.20)
        {
            result.IsValid = false;
            result.Message = $"价格偏离{deviation:P1}（前日{yesterday.Settle} -> 今日{today.Settle}）";
        }

        return result;
    }

    /// <summary>IV 范围检查</summary>
    public bool IsIvReasonable(double iv) => iv >= 0.05 && iv <= 2.0;
}