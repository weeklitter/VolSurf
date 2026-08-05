using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VolSurf.Data.Entities;

namespace VolSurf.Data.Configurations;

public class StockBasicConfiguration : IEntityTypeConfiguration<StockBasic>
{
    public void Configure(EntityTypeBuilder<StockBasic> builder)
    {
        builder.ToTable("stock_basic");
        builder.HasKey(x => x.TsCode);
        builder.Property(x => x.TsCode).HasMaxLength(20);
        builder.Property(x => x.Symbol).HasMaxLength(10);
        builder.Property(x => x.Name).HasMaxLength(50);
        builder.Property(x => x.Area).HasMaxLength(20);
        builder.Property(x => x.Industry).HasMaxLength(50);
        builder.Property(x => x.Market).HasMaxLength(20);
        builder.Property(x => x.ListDate).HasColumnType("date");
        builder.Property(x => x.Exchange).HasMaxLength(10);
    }
}
