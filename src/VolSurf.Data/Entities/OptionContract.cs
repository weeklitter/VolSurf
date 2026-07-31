namespace VolSurf.Data.Entities;

public class OptionContract
{
    public string TsCode { get; set; } = default!;
    public string Symbol { get; set; } = default!;
    public string Exchange { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string Underlying { get; set; } = default!;
    public string CallPut { get; set; } = default!;  // "C" / "P"
    public decimal ExercisePrice { get; set; }
    public string ExerciseType { get; set; } = default!;
    public decimal OptMultiplier { get; set; }
    public DateTime MaturityDate { get; set; }
    public DateTime? ListDate { get; set; }
    public DateTime? DelistDate { get; set; }
    public bool Adjusted { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}