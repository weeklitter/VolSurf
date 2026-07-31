namespace VolSurf.Core.Options;

/// <summary>
/// 无风险利率配置（MVP 阶段用 appsettings.json 注入默认 0.02）
/// </summary>
public class RiskFreeRateOptions
{
    public const string SectionName = "RiskFreeRate";

    public double DefaultRate { get; set; } = 0.02;
    public string Description { get; set; } = "1年期国债收益率，MVP默认2.0%";
}