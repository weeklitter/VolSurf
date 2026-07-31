using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VolSurf.Data.Entities;

namespace VolSurf.Data.Configurations;

public class OptionDailyConfiguration : IEntityTypeConfiguration<OptionDaily>
{
    public void Configure(EntityTypeBuilder<OptionDaily> builder)
    {
        builder.ToTable("options_daily");
        builder.HasKey(x => new { x.TsCode, x.TradeDate });
        builder.Property(x => x.TsCode).HasMaxLength(30);
        builder.Property(x => x.Underlying).HasMaxLength(20);
        builder.Property(x => x.TradeDate).HasColumnType("date");
        builder.Property(x => x.Open).HasColumnType("decimal(10,4)");
        builder.Property(x => x.High).HasColumnType("decimal(10,4)");
        builder.Property(x => x.Low).HasColumnType("decimal(10,4)");
        builder.Property(x => x.Close).HasColumnType("decimal(10,4)");
        builder.Property(x => x.Settle).HasColumnType("decimal(10,4)");
        builder.Property(x => x.Vol).HasColumnType("decimal(15,2)");
        builder.Property(x => x.Amount).HasColumnType("decimal(15,4)");
        builder.Property(x => x.Oi).HasColumnType("decimal(15,2)");
        builder.HasIndex(x => x.TradeDate);
        builder.HasIndex(x => new { x.Underlying, x.TradeDate });
        builder.HasIndex(x => new { x.TsCode, x.TradeDate });
    }
}