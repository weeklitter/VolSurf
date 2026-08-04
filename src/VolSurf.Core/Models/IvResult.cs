namespace VolSurf.Core.Models;

/// <summary>
/// IV 计算结果模型
/// </summary>
public class IvResult
{
    public double? Iv { get; set; }
    public double? Delta { get; set; }
    public double? Gamma { get; set; }
    public double? Theta { get; set; }  // 每日 Theta（已 /365）
    public double? Vega { get; set; }   // 每 1% 波动率（已 /100）
    public double? Rho { get; set; }    // 每 1% 利率（已 /100）
    public bool Confidence { get; set; }
    public bool Anomaly { get; set; }
    public string? SkipReason { get; set; }

    public static IvResult Empty(string reason) => new()
    {
        Confidence = false,
        SkipReason = reason
    };

    public static IvResult AnomalyValue(double iv, string reason) => new()
    {
        Iv = iv,
        Anomaly = true,
        Confidence = false,
        SkipReason = reason
    };
}