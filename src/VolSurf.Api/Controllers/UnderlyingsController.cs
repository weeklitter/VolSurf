using Microsoft.AspNetCore.Mvc;
using VolSurf.Core.Models;
using VolSurf.Data.Entities;
using VolSurf.Data.Repositories;

namespace VolSurf.Api.Controllers;

/// <summary>
/// 标的基础信息接口：列出所有支持的标的（ETF/股指）。
/// </summary>
[ApiController]
[Route("api/underlyings")]
public class UnderlyingsController(IUnderlyingRepository repo) : ControllerBase
{
    /// <summary>GET /api/underlyings - 获取所有标的（按 sort_order 升序）</summary>
    [HttpGet]
    [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> GetAll()
    {
        var list = await repo.GetAllUnderlyingsAsync();
        var data = list.Select(u => new
        {
            tsCode = u.TsCode,
            name = u.Name,
            exchange = u.Exchange,
            assetClass = u.AssetClass
        }).ToList();
        return Ok(ApiResponse<object>.Ok(data));
    }

    /// <summary>GET /api/underlyings/{tsCode} - 获取单个标的</summary>
    [HttpGet("{tsCode}")]
    public async Task<IActionResult> GetOne(string tsCode)
    {
        var ul = await repo.GetUnderlyingAsync(tsCode);
        if (ul == null)
            throw new KeyNotFoundException($"未找到标的 {tsCode}");

        return Ok(ApiResponse<Underlying>.Ok(ul));
    }
}