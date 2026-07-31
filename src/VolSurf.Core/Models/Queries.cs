using System.ComponentModel.DataAnnotations;

namespace VolSurf.Core.Models;

/// <summary>期权链查询参数</summary>
public class OptionChainQuery
{
    [Required(ErrorMessage = "underlying参数不能为空")]
    [RegularExpression(@"^(510050|510300|000300)$",
        ErrorMessage = "underlying必须是510050/510300/000300之一")]
    public string Underlying { get; set; } = default!;

    [Required(ErrorMessage = "expiry参数不能为空")]
    public DateTime Expiry { get; set; }

    public DateTime? Date { get; set; }  // 不传则取最新交易日
}

/// <summary>波动率曲面查询参数</summary>
public class VolSurfaceQuery
{
    [Required]
    [RegularExpression(@"^(510050|510300|000300)$")]
    public string Underlying { get; set; } = default!;

    public DateTime? Date { get; set; }
}

/// <summary>波动率微笑曲线查询参数</summary>
public class VolSmileQuery
{
    [Required]
    [RegularExpression(@"^(510050|510300|000300)$")]
    public string Underlying { get; set; } = default!;

    [Required]
    public DateTime Expiry { get; set; }

    public DateTime? Date { get; set; }
}

/// <summary>期限结构查询参数</summary>
public class TermStructureQuery
{
    [Required]
    [RegularExpression(@"^(510050|510300|000300)$")]
    public string Underlying { get; set; } = default!;

    public DateTime? Date { get; set; }
}

/// <summary>IV 百分位查询参数</summary>
public class IvPercentileQuery
{
    [Required]
    [RegularExpression(@"^(510050|510300|000300)$")]
    public string Underlying { get; set; } = default!;
}

/// <summary>交易日列表查询参数</summary>
public class TradeDatesQuery
{
    [Required]
    [RegularExpression(@"^(510050|510300|000300)$")]
    public string Underlying { get; set; } = default!;

    [Range(1, 365)]
    public int Limit { get; set; } = 30;
}

/// <summary>到期月列表查询参数</summary>
public class ExpiriesQuery
{
    [Required]
    [RegularExpression(@"^(510050|510300|000300)$")]
    public string Underlying { get; set; } = default!;

    public DateTime? Date { get; set; }
}

/// <summary>内部触发计算请求</summary>
public class TriggerCalcRequest
{
    [Required]
    public DateTime TradeDate { get; set; }
}