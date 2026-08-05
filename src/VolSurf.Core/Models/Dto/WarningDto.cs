namespace VolSurf.Core.Models.Dto;

/// <summary>异常预警</summary>
public class WarningDto
{
    public string Type { get; set; } = default!;          // 规则类型标识
    public string Level { get; set; } = "info";           // info/warn/danger
    public string Message { get; set; } = default!;       // 预警消息
    public double Value { get; set; }                     // 当前值
    public double Threshold { get; set; }                 // 阈值
}
