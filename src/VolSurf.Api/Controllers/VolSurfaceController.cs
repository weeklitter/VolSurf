using Microsoft.AspNetCore.Mvc;
using VolSurf.Core.Models;
using VolSurf.Core.Models.Dto;
using VolSurf.Core.Services;
using VolSurf.Data.Repositories;

namespace VolSurf.Api.Controllers;

/// <summary>
/// 波动率分析接口：3D 曲面 / 微笑曲线 / 期限结构 / IV 百分位。
/// </summary>
[ApiController]
[Route("api")]
public class VolSurfaceController(
    VolSurfaceService surfaceService,
    IUnderlyingRepository ulRepo) : ControllerBase
{
    /// <summary>GET /api/vol-surface?underlying=510050&amp;date=2026-07-31</summary>
    [HttpGet("vol-surface")]
    [ResponseCache(Duration = 300, VaryByQueryKeys = new[] { "underlying", "date" })]
    public async Task<IActionResult> GetVolSurface([FromQuery] VolSurfaceQuery query)
    {
        var date = query.Date?.Date
            ?? (await ResolveLatestDateAsync(query.Underlying));
        var data = await surfaceService.GetVolSurfaceAsync(query.Underlying, date);
        return Ok(ApiResponse<VolSurfaceDto>.Ok(data));
    }

    /// <summary>GET /api/vol-smile?underlying=510050&amp;expiry=2026-09-22&amp;date=2026-07-31</summary>
    [HttpGet("vol-smile")]
    [ResponseCache(Duration = 300, VaryByQueryKeys = new[] { "underlying", "expiry", "date" })]
    public async Task<IActionResult> GetVolSmile([FromQuery] VolSmileQuery query)
    {
        var date = query.Date?.Date
            ?? (await ResolveLatestDateAsync(query.Underlying));
        var data = await surfaceService.GetVolSmileAsync(
            query.Underlying, date, query.Expiry.Date);
        return Ok(ApiResponse<VolSmileDto>.Ok(data));
    }

    /// <summary>GET /api/term-structure?underlying=510050&amp;date=2026-07-31</summary>
    [HttpGet("term-structure")]
    [ResponseCache(Duration = 300, VaryByQueryKeys = new[] { "underlying", "date" })]
    public async Task<IActionResult> GetTermStructure([FromQuery] TermStructureQuery query)
    {
        var date = query.Date?.Date
            ?? (await ResolveLatestDateAsync(query.Underlying));
        var data = await surfaceService.GetTermStructureAsync(query.Underlying, date);
        return Ok(ApiResponse<TermStructureDto>.Ok(data));
    }

    /// <summary>GET /api/iv-percentile?underlying=510050</summary>
    [HttpGet("iv-percentile")]
    [ResponseCache(Duration = 300, VaryByQueryKeys = new[] { "underlying" })]
    public async Task<IActionResult> GetIvPercentile([FromQuery] IvPercentileQuery query)
    {
        var latest = await ulRepo.GetIvPercentileAsync(query.Underlying)
            ?? throw new KeyNotFoundException($"未找到 {query.Underlying} 的 IV 百分位数据");

        var data = new
        {
            underlying = latest.Underlying,
            tradeDate = latest.TradeDate.ToString("yyyy-MM-dd"),
            atmIv = latest.AtmIv,
            ivPercentile = latest.IvPercentile,
            ivMean = latest.IvMean,
            ivStd = latest.IvStd,
            sampleDays = latest.SampleDays
        };
        return Ok(ApiResponse<object>.Ok(data));
    }

    private async Task<DateTime> ResolveLatestDateAsync(string underlying)
    {
        var ul = await ulRepo.GetLatestUnderlyingDailyAsync(underlying)
            ?? throw new KeyNotFoundException($"未找到 {underlying} 的标的行情");
        return ul.TradeDate.Date;
    }
}