using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VolSurf.Data.Entities;

namespace VolSurf.Data.Configurations;

public class StockCashflowConfiguration : IEntityTypeConfiguration<StockCashflow>
{
    public void Configure(EntityTypeBuilder<StockCashflow> builder)
    {
        builder.ToTable("stock_cashflow");
        builder.HasKey(x => new { x.TsCode, x.EndDate, x.ReportType });
        builder.Property(x => x.TsCode).HasMaxLength(20);
        builder.Property(x => x.EndDate).HasColumnType("date");
        builder.Property(x => x.ReportType).HasMaxLength(2);
        builder.Property(x => x.OperCashFlow).HasColumnType("decimal(20,4)");
        builder.Property(x => x.InvestCashFlow).HasColumnType("decimal(20,4)");
        builder.Property(x => x.FinCashFlow).HasColumnType("decimal(20,4)");
        builder.Property(x => x.CapEx).HasColumnType("decimal(20,4)");
        builder.Property(x => x.UpdateDate).HasColumnType("date");
        builder.HasIndex(x => x.EndDate);
    }
}
