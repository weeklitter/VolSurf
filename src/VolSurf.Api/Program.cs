using System.Reflection;
using System.Threading.Channels;
using System.Threading.RateLimiting;
using AspNetCoreRateLimit;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;
using VolSurf.Api.BackgroundServices;
using VolSurf.Api.Middleware;
using VolSurf.Core.Options;
using VolSurf.Core.Services;
using VolSurf.Data;
using VolSurf.Data.Repositories;

var builder = WebApplication.CreateBuilder(args);

// ═══════════════════════════════════════════════════════════════════════════
// Serilog
// ═══════════════════════════════════════════════════════════════════════════
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "VolSurf.Api")
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

// ═══════════════════════════════════════════════════════════════════════════
// 配置 / Options
// ═══════════════════════════════════════════════════════════════════════════
builder.Services.Configure<RiskFreeRateOptions>(
    builder.Configuration.GetSection(RiskFreeRateOptions.SectionName));
builder.Services.Configure<InternalKeyOptions>(
    builder.Configuration.GetSection(InternalKeyOptions.SectionName));

// ═══════════════════════════════════════════════════════════════════════════
// EF Core / PostgreSQL
// ═══════════════════════════════════════════════════════════════════════════
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings:Default 未配置");

builder.Services.AddDbContext<VolSurfDbContext>(opt =>
    opt.UseNpgsql(connectionString, npg =>
    {
        npg.MigrationsAssembly("VolSurf.Data");
        npg.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), null);
    }));

// ═══════════════════════════════════════════════════════════════════════════
// Repositories / Services（Scoped）
// ═══════════════════════════════════════════════════════════════════════════
builder.Services.AddScoped<IOptionRepository, OptionRepository>();
builder.Services.AddScoped<IUnderlyingRepository, UnderlyingRepository>();
builder.Services.AddScoped<IvCalculationService>();
builder.Services.AddScoped<DataValidationService>();
builder.Services.AddScoped<IvPercentileService>();
builder.Services.AddScoped<VolSurfaceService>();

// ── 股票模块 ──
builder.Services.AddScoped<IStockRepository, StockRepository>();
builder.Services.AddScoped<StockAnalysisService>();
builder.Services.AddScoped<ValuationService>();
builder.Services.AddScoped<MarketService>();
builder.Services.AddScoped<WarningService>();
// ScoreEngine 是静态类，无需 DI 注册

// ═══════════════════════════════════════════════════════════════════════════
// Channel<T> + BackgroundService（计算服务寄宿在 API 进程内）
// ═══════════════════════════════════════════════════════════════════════════
builder.Services.AddSingleton(_ =>
    Channel.CreateBounded<CalcTask>(new BoundedChannelOptions(100)
    {
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = false,
        SingleWriter = false
    }));
builder.Services.AddHostedService<CalcBackgroundService>();

// 批量回算 Channel（单 reader，bound=1，避免并发回算冲突）
builder.Services.AddSingleton(_ =>
    Channel.CreateBounded<BulkBackfillRequest>(new BoundedChannelOptions(10)
    {
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = true,
        SingleWriter = false
    }));
builder.Services.AddHostedService<BulkBackfillBackgroundService>();

// ═══════════════════════════════════════════════════════════════════════════
// ASP.NET Core 组件
// ═══════════════════════════════════════════════════════════════════════════
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "VolSurf API",
        Version = "v1",
        Description = "波动率曲面分析 API"
    });
});

// CORS
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:3000" };

builder.Services.AddCors(opt =>
{
    opt.AddDefaultPolicy(policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials());
});

// ResponseCaching
builder.Services.AddResponseCaching(opt =>
{
    opt.MaximumBodySize = 10 * 1024 * 1024; // 10MB（3D 曲面可能较大）
});

// Rate limiting（基于 AspNetCoreRateLimit）
builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(
    builder.Configuration.GetSection("IpRateLimitOptions"));
builder.Services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();
builder.Services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
builder.Services.AddInMemoryRateLimiting();

// Health checks
builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "postgres");

var app = builder.Build();

// ═══════════════════════════════════════════════════════════════════════════
// Middleware pipeline（顺序敏感）
// ═══════════════════════════════════════════════════════════════════════════
app.UseMiddleware<GlobalExceptionMiddleware>();

// Serilog 请求日志
app.UseSerilogRequestLogging(opt =>
{
    opt.MessageTemplate = "HTTP {RequestMethod} {RequestPath} -> {StatusCode} in {Elapsed:0.0000}ms";
});

// Swagger（开发环境启用）
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();

// Rate limiting（IP）
app.UseIpRateLimiting();

app.UseResponseCaching();

app.UseRouting();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

// 根路径健康提示
app.MapGet("/", () => Results.Ok(new
{
    service = "VolSurf.Api",
    version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0",
    status = "ok"
}));

try
{
    // 启动时自动执行 EF Core 迁移（生产环境便捷部署）
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<VolSurf.Data.VolSurfDbContext>();
        try
        {
            await db.Database.MigrateAsync();
            Log.Information("EF Core migrations applied");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "EF Core migration failed");
            throw;
        }
    }

    Log.Information("Starting VolSurf.Api");
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "VolSurf.Api terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}