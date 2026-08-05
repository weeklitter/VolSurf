using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VolSurf.Data.Entities;

namespace VolSurf.Data.Configurations;

public class StockBusinessConfiguration : IEntityTypeConfiguration<StockBusiness>
{
    public void Configure(EntityTypeBuilder<StockBusiness> builder)
    {
        builder.ToTable("stock_business");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TsCode).HasMaxLength(20);
        builder.Property(x => x.EndDate).HasColumnType("date");
        builder.Property(x => x.BusinessItem).HasMaxLength(100);
        builder.Property(x => x.MainType).HasMaxLength(2);
        builder.Property(x => x.Revenue).HasColumnType("decimal(20,4)");
        builder.Property(x => x.Cost).HasColumnType("decimal(20,4)");
        builder.Property(x => x.Profit).HasColumnType("decimal(20,4)");
        builder.Property(x => x.Ratio).HasColumnType("decimal(8,4)");
        // 唯一索引
        builder.HasIndex(x => new { x.TsCode, x.EndDate, x.BusinessItem, x.MainType })
               .IsUnique();
        builder.HasIndex(x => new { x.TsCode, x.EndDate });
    }
}
