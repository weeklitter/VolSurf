using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VolSurf.Data.Entities;

namespace VolSurf.Data.Configurations;

public class OptionContractConfiguration : IEntityTypeConfiguration<OptionContract>
{
    public void Configure(EntityTypeBuilder<OptionContract> builder)
    {
        builder.ToTable("options_contracts");
        builder.HasKey(x => x.TsCode);
        builder.Property(x => x.TsCode).HasMaxLength(30);
        builder.Property(x => x.Symbol).HasMaxLength(20);
        builder.Property(x => x.Exchange).HasMaxLength(10);
        builder.Property(x => x.Name).HasMaxLength(50);
        builder.Property(x => x.Underlying).HasMaxLength(20);
        builder.Property(x => x.CallPut).HasColumnType("char(1)");
        builder.Property(x => x.ExercisePrice).HasColumnType("decimal(10,4)");
        builder.Property(x => x.ExerciseType).HasMaxLength(10);
        builder.Property(x => x.OptMultiplier).HasColumnType("decimal(10,4)");
        builder.Property(x => x.MaturityDate).HasColumnType("date");
        builder.Property(x => x.ListDate).HasColumnType("date");
        builder.Property(x => x.DelistDate).HasColumnType("date");
        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz");
        builder.Property(x => x.UpdatedAt).HasColumnType("timestamptz");
        builder.HasIndex(x => x.Underlying);
        builder.HasIndex(x => x.MaturityDate);
    }
}