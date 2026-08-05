namespace VolSurf.Core.Models.Dto;

/// <summary>股票分析聚合报告（核心返回结构）</summary>
public class StockAnalysisReportDto
{
    public string TsCode { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string Industry { get; set; } = default!;
    public string ReportDate { get; set; } = default!;  // 数据截止日期

    public FinancialMetricsDto Financial { get; set; } = new();
    public ValuationMetricsDto Valuation { get; set; } = new();
    public MarketMetricsDto Market { get; set; } = new();
    public BusinessCompositionDto Business { get; set; } = new();
    public List<WarningDto> Warnings { get; set; } = new();

    // 综合评分
    public double HealthScore { get; set; }
    public double GrowthScore { get; set; }
    public double ValueScore { get; set; }
    public double OverallScore { get; set; }

    // 预留 AI 解读接口（方式B）
    public AiAnalysisResultDto? AiAnalysis { get; set; }
}

/// <summary>AI分析结果（预留，本期不实现）</summary>
public class AiAnalysisResultDto
{
    public string Summary { get; set; } = default!;   // 一句话总结
    public List<string> Strengths { get; set; } = new(); // 亮点
    public List<string> Risks { get; set; } = new();     // 风险点
}
