using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VolSurf.Core.Models;
using VolSurf.Core.Models.Dto;
using VolSurf.Data;
using VolSurf.Data.Entities;
using VolSurf.Data.Repositories;

namespace VolSurf.Api.Controllers;

/// <summary>
/// 期权链 / 交易日 / 到期月接口。
/// </summary>
[ApiController]
[Route("api")]
public class OptionChainController(
    IOptionRepository optRepo,
    IUnderlyingRepository ulRepo,
    VolSurfDbContext db) : ControllerBase
{
    /// <summary>GET /api/option-chain?underlying=510050&amp;expiry=2026-09-22&amp;date=2026-07-31</summary>
    [HttpGet("option-chain")]
    [ResponseCache(Duration = 300, VaryByQueryKeys = new[] { "underlying", "expiry", "date" })]
    public async Task<IActionResult> GetOptionChain([FromQuery] OptionChainQuery query)
    {
        // 不传 date → 取最新交易日
        DateTime tradeDate;
        if (query.Date.HasValue)
        {
            tradeDate = query.Date.Value.Date;
        }
        else
        {
            var latest = await optRepo.GetLatestTradeDateAsync(query.Underlying);
            if (latest == null)
                throw new KeyNotFoundException($"未找到 {query.Underlying} 的期权数据");
            tradeDate = latest.Value.Date;
        }

        var expiry = query.Expiry.Date;
        var underlying = await ulRepo.GetUnderlyingAsync(query.Underlying)
            ?? throw new KeyNotFoundException($"未找到标的 {query.Underlying}");

        var ulDaily = await ulRepo.GetLatestUnderlyingDailyAsync(query.Underlying);
        decimal underlyingPrice = ulDaily?.Close ?? 0m;

        // 当日到期月合约
        var contracts = await optRepo.GetContractsByExpiryAsync(query.Underlying, expiry);
        if (contracts.Count == 0)
            throw new KeyNotFoundException($"未找到 {query.Underlying} {expiry:yyyy-MM-dd} 到期月的合约");

        var contractCodes = contracts.Select(c => c.TsCode).ToHashSet();

        // 当日 IV/Greeks
        var ivData = await optRepo.GetIvGreeksAsync(tradeDate, query.Underlying);
        var ivMap = ivData
            .Where(x => contractCodes.Contains(x.TsCode))
            .ToDictionary(x => x.TsCode);

        // 当日行情
        var dailyRecords = await db.OptionDaily
            .Where(d => d.TradeDate == tradeDate && d.Underlying == query.Underlying)
            .Where(d => contractCodes.Contains(d.TsCode))
            .ToListAsync();
        var dailyMap = dailyRecords.ToDictionary(d => d.TsCode);

        var calls = new List<OptionContractDto>();
        var puts = new List<OptionContractDto>();
        foreach (var c in contracts)
        {
            dailyMap.TryGetValue(c.TsCode, out var daily);
            ivMap.TryGetValue(c.TsCode, out var iv);

            var dto = new OptionContractDto
            {
                TsCode = c.TsCode,
                Strike = c.ExercisePrice,
                Price = daily?.Close,
                Settle = daily?.Settle,
                Volume = daily?.Vol,
                OpenInterest = daily?.Oi,
                Iv = iv?.Iv,
                Delta = iv?.Delta,
                IvConfidence = iv?.IvConfidence ?? false
            };
            if (c.CallPut == "C") calls.Add(dto);
            else puts.Add(dto);
        }

        // IV 百分位
        var percentile = await ulRepo.GetIvPercentileAsync(query.Underlying);
        decimal? ivPercentile = null;
        if (percentile != null && percentile.TradeDate.Date == tradeDate)
        {
            ivPercentile = percentile.IvPercentile;
        }

        var data = new OptionChainDto
        {
            Underlying = new UnderlyingInfo
            {
                TsCode = underlying.TsCode,
                Name = underlying.Name,
                Price = underlyingPrice
            },
            TradeDate = tradeDate.ToString("yyyy-MM-dd"),
            Expiry = expiry.ToString("yyyy-MM-dd"),
            IvPercentile = ivPercentile,
            Calls = calls.OrderBy(x => x.Strike).ToList(),
            Puts = puts.OrderBy(x => x.Strike).ToList()
        };

        return Ok(ApiResponse<OptionChainDto>.Ok(data));
    }

    /// <summary>GET /api/trade-dates?underlying=510050&amp;limit=30</summary>
    [HttpGet("trade-dates")]
    [ResponseCache(Duration = 300, VaryByQueryKeys = new[] { "underlying", "limit" })]
    public async Task<IActionResult> GetTradeDates([FromQuery] TradeDatesQuery query)
    {
        var dates = await optRepo.GetTradeDatesAsync(query.Underlying, query.Limit);
        var list = dates.Select(d => d.ToString("yyyy-MM-dd")).ToList();
        return Ok(ApiResponse<List<string>>.Ok(list));
    }

    /// <summary>GET /api/expiries?underlying=510050&amp;date=2026-07-31</summary>
    [HttpGet("expiries")]
    [ResponseCache(Duration = 300, VaryByQueryKeys = new[] { "underlying", "date" })]
    public async Task<IActionResult> GetExpiries([FromQuery] ExpiriesQuery query)
    {
        var date = query.Date?.Date ?? DateTime.UtcNow.Date;
        var expiries = await optRepo.GetAvailableExpiriesAsync(query.Underlying, date);
        var list = expiries.Select(d => d.ToString("yyyy-MM-dd")).ToList();
        return Ok(ApiResponse<List<string>>.Ok(list));
    }
}