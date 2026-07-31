using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VolSurf.Data.Entities;

namespace VolSurf.Data.Configurations;

public class UnderlyingConfiguration : IEntityTypeConfiguration<Underlying>
{
    public void Configure(EntityTypeBuilder<Underlying> builder)
    {
        builder.ToTable("underlyings");
        builder.HasKey(x => x.TsCode);
        builder.Property(x => x.TsCode).HasMaxLength(20);
        builder.Property(x => x.Name).HasMaxLength(50);
        builder.Property(x => x.Exchange).HasMaxLength(10);
        builder.Property(x => x.AssetClass).HasMaxLength(10);
    }
}