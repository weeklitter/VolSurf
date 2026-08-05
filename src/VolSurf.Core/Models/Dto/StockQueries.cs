using System.ComponentModel.DataAnnotations;

namespace VolSurf.Core.Models.Dto;

/// <summary>股票搜索查询参数</summary>
public class StockSearchQuery
{
    [Required(ErrorMessage = "q参数不能为空")]
    public string Q { get; set; } = default!;

    [Range(1, 50)]
    public int Limit { get; set; } = 20;
}

/// <summary>股票列表查询参数</summary>
public class StockListQuery
{
    public string? Industry { get; set; }

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int Size { get; set; } = 20;
}

/// <summary>股票行情查询参数</summary>
public class StockDailyQuery
{
    [Required(ErrorMessage = "start参数不能为空")]
    public DateTime Start { get; set; }

    [Required(ErrorMessage = "end参数不能为空")]
    public DateTime End { get; set; }
}
