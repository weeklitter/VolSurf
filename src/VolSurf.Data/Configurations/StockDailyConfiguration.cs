using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VolSurf.Data.Entities;

namespace VolSurf.Data.Configurations;

public class StockDailyConfiguration : IEntityTypeConfiguration<StockDaily>
{
    public void Configure(EntityTypeBuilder<StockDaily> builder)
    {
        builder.ToTable("stock_daily");
        builder.HasKey(x => new { x.TsCode, x.TradeDate });
        builder.Property(x => x.TsCode).HasMaxLength(20);
        builder.Property(x => x.TradeDate).HasColumnType("date");
        builder.Property(x => x.Open).HasColumnType("decimal(10,4)");
        builder.Property(x => x.High).HasColumnType("decimal(10,4)");
        builder.Property(x => x.Low).HasColumnType("decimal(10,4)");
        builder.Property(x => x.Close).HasColumnType("decimal(10,4)");
        builder.Property(x => x.PreClose).HasColumnType("decimal(10,4)");
        builder.Property(x => x.Change).HasColumnType("decimal(10,4)");
        builder.Property(x => x.PctChg).HasColumnType("decimal(8,4)");
        builder.Property(x => x.Vol).HasColumnType("decimal(15,2)");
        builder.Property(x => x.Amount).HasColumnType("decimal(15,4)");
        builder.HasIndex(x => x.TradeDate);
    }
}
