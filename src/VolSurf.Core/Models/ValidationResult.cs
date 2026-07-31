namespace VolSurf.Core.Models;

/// <summary>
/// 数据校验结果
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; set; }
    public string? Message { get; set; }
    public List<string> Anomalies { get; set; } = new();
}