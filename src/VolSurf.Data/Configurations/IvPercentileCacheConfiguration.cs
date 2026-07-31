using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VolSurf.Data.Entities;

namespace VolSurf.Data.Configurations;

public class IvPercentileCacheConfiguration : IEntityTypeConfiguration<IvPercentileCache>
{
    public void Configure(EntityTypeBuilder<IvPercentileCache> builder)
    {
        builder.ToTable("iv_percentile_cache");
        builder.HasKey(x => new { x.Underlying, x.TradeDate });
        builder.Property(x => x.Underlying).HasMaxLength(20);
        builder.Property(x => x.TradeDate).HasColumnType("date");
        builder.Property(x => x.AtmIv).HasColumnType("decimal(8,4)");
        builder.Property(x => x.IvPercentile).HasColumnType("decimal(5,2)");
        builder.Property(x => x.IvMean).HasColumnType("decimal(8,4)");
        builder.Property(x => x.IvStd).HasColumnType("decimal(8,4)");
    }
}