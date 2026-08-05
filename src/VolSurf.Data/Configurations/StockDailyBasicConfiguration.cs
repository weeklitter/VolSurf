using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VolSurf.Data.Entities;

namespace VolSurf.Data.Configurations;

public class StockDailyBasicConfiguration : IEntityTypeConfiguration<StockDailyBasic>
{
    public void Configure(EntityTypeBuilder<StockDailyBasic> builder)
    {
        builder.ToTable("stock_daily_basic");
        builder.HasKey(x => new { x.TsCode, x.TradeDate });
        builder.Property(x => x.TsCode).HasMaxLength(20);
        builder.Property(x => x.TradeDate).HasColumnType("date");
        builder.Property(x => x.Close).HasColumnType("decimal(10,4)");
        builder.Property(x => x.Pe).HasColumnType("decimal(12,4)");
        builder.Property(x => x.PeTtm).HasColumnType("decimal(12,4)");
        builder.Property(x => x.Pb).HasColumnType("decimal(12,4)");
        builder.Property(x => x.Ps).HasColumnType("decimal(12,4)");
        builder.Property(x => x.PsTtm).HasColumnType("decimal(12,4)");
        builder.Property(x => x.TotalMv).HasColumnType("decimal(15,4)");
        builder.Property(x => x.CircMv).HasColumnType("decimal(15,4)");
        builder.Property(x => x.TurnoverRate).HasColumnType("decimal(8,4)");
        builder.Property(x => x.DvRatio).HasColumnType("decimal(8,4)");
        builder.HasIndex(x => x.TradeDate);
    }
}
