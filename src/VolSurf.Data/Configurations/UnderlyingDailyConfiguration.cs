using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VolSurf.Data.Entities;

namespace VolSurf.Data.Configurations;

public class UnderlyingDailyConfiguration : IEntityTypeConfiguration<UnderlyingDaily>
{
    public void Configure(EntityTypeBuilder<UnderlyingDaily> builder)
    {
        builder.ToTable("underlying_daily");
        builder.HasKey(x => new { x.TsCode, x.TradeDate });
        builder.Property(x => x.TsCode).HasMaxLength(20);
        builder.Property(x => x.TradeDate).HasColumnType("date");
        builder.Property(x => x.Close).HasColumnType("decimal(10,4)");
    }
}