namespace VolSurf.Core.Options;

/// <summary>
/// 内部接口共享密钥（Python -> .NET 内部触发计算）
/// </summary>
public class InternalKeyOptions
{
    public const string SectionName = "InternalKey";

    public string Key { get; set; } = string.Empty;
}