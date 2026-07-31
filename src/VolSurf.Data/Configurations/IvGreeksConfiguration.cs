using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VolSurf.Data.Entities;

namespace VolSurf.Data.Configurations;

public class IvGreeksConfiguration : IEntityTypeConfiguration<IvGreeks>
{
    public void Configure(EntityTypeBuilder<IvGreeks> builder)
    {
        builder.ToTable("options_iv_greeks");
        builder.HasKey(x => new { x.TsCode, x.TradeDate });
        builder.Property(x => x.TsCode).HasMaxLength(30);
        builder.Property(x => x.Underlying).HasMaxLength(20);
        builder.Property(x => x.TradeDate).HasColumnType("date");
        builder.Property(x => x.Iv).HasColumnType("decimal(8,4)");
        builder.Property(x => x.Delta).HasColumnType("decimal(8,4)");
        builder.Property(x => x.Gamma).HasColumnType("decimal(8,4)");
        builder.Property(x => x.Theta).HasColumnType("decimal(10,4)");
        builder.Property(x => x.Vega).HasColumnType("decimal(8,4)");
        builder.Property(x => x.Rho).HasColumnType("decimal(8,4)");
        builder.HasIndex(x => new { x.Underlying, x.TradeDate });
        builder.HasIndex(x => new { x.TradeDate, x.Underlying });
    }
}