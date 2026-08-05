using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VolSurf.Data.Entities;

namespace VolSurf.Data.Configurations;

public class StockIncomeConfiguration : IEntityTypeConfiguration<StockIncome>
{
    public void Configure(EntityTypeBuilder<StockIncome> builder)
    {
        builder.ToTable("stock_income");
        builder.HasKey(x => new { x.TsCode, x.EndDate, x.ReportType });
        builder.Property(x => x.TsCode).HasMaxLength(20);
        builder.Property(x => x.EndDate).HasColumnType("date");
        builder.Property(x => x.ReportType).HasMaxLength(2);
        builder.Property(x => x.Revenue).HasColumnType("decimal(20,4)");
        builder.Property(x => x.OperCost).HasColumnType("decimal(20,4)");
        builder.Property(x => x.GrossProfit).HasColumnType("decimal(20,4)");
        builder.Property(x => x.NetProfit).HasColumnType("decimal(20,4)");
        builder.Property(x => x.UpdateDate).HasColumnType("date");
        builder.HasIndex(x => x.EndDate);
    }
}
