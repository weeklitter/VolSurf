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

    // 股票分析模块
    public DbSet<StockBasic> StockBasic => Set<StockBasic>();
    public DbSet<StockDaily> StockDaily => Set<StockDaily>();
    public DbSet<StockIncome> StockIncome => Set<StockIncome>();
    public DbSet<StockBalanceSheet> StockBalanceSheet => Set<StockBalanceSheet>();
    public DbSet<StockCashflow> StockCashflow => Set<StockCashflow>();
    public DbSet<StockBusiness> StockBusiness => Set<StockBusiness>();
    public DbSet<StockDailyBasic> StockDailyBasic => Set<StockDailyBasic>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // 自动应用所有 IEntityTypeConfiguration
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(VolSurfDbContext).Assembly);
    }
}