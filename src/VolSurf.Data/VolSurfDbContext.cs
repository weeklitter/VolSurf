using Microsoft.EntityFrameworkCore;
using VolSurf.Data.Entities;

namespace VolSurf.Data;

public class VolSurfDbContext(DbContextOptions<VolSurfDbContext> options) : DbContext(options)
{
    public DbSet<OptionContract> OptionContracts => Set<OptionContract>();
    public DbSet<OptionDaily> OptionDaily => Set<OptionDaily>();
    public DbSet<IvGreeks> IvGreeks => Set<IvGreeks>();
    public DbSet<Underlying> Underlyings => Set<Underlying>();
    public DbSet<UnderlyingDaily> UnderlyingDaily => Set<UnderlyingDaily>();
    public DbSet<IvPercentileCache> IvPercentileCache => Set<IvPercentileCache>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // 自动应用所有 IEntityTypeConfiguration
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(VolSurfDbContext).Assembly);
    }
}