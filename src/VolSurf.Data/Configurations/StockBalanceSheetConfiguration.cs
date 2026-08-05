using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VolSurf.Data.Entities;

namespace VolSurf.Data.Configurations;

public class StockBalanceSheetConfiguration : IEntityTypeConfiguration<StockBalanceSheet>
{
    public void Configure(EntityTypeBuilder<StockBalanceSheet> builder)
    {
        builder.ToTable("stock_balance_sheet");
        builder.HasKey(x => new { x.TsCode, x.EndDate, x.ReportType });
        builder.Property(x => x.TsCode).HasMaxLength(20);
        builder.Property(x => x.EndDate).HasColumnType("date");
        builder.Property(x => x.ReportType).HasMaxLength(2);
        builder.Property(x => x.TotalAssets).HasColumnType("decimal(20,4)");
        builder.Property(x => x.TotalLiab).HasColumnType("decimal(20,4)");
        builder.Property(x => x.TotalEquity).HasColumnType("decimal(20,4)");
        builder.Property(x => x.Goodwill).HasColumnType("decimal(20,4)");
        builder.Property(x => x.AccountRecv).HasColumnType("decimal(20,4)");
        builder.Property(x => x.Inventory).HasColumnType("decimal(20,4)");
        builder.Property(x => x.UpdateDate).HasColumnType("date");
        builder.HasIndex(x => x.EndDate);
    }
}
