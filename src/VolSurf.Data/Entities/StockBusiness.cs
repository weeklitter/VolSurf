namespace VolSurf.Data.Entities;

/// <summary>主营业务构成</summary>
public class StockBusiness
{
    public int Id { get; set; }                        // 自增主键
    public string TsCode { get; set; } = default!;
    public DateTime EndDate { get; set; }
    public string BusinessItem { get; set; } = default!; // 业务项名称
    public string MainType { get; set; } = default!;    // P=产品 D=地区
    public decimal? Revenue { get; set; }               // 营业收入
    public decimal? Cost { get; set; }                  // 营业成本
    public decimal? Profit { get; set; }                // 毛利润
    public decimal? Ratio { get; set; }                 // 占比(%)
}
