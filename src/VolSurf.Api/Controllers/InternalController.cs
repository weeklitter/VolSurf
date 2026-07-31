using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using VolSurf.Api.BackgroundServices;
using VolSurf.Core.Models;
using VolSurf.Core.Options;
using VolSurf.Data.Repositories;

namespace VolSurf.Api.Controllers;

/// <summary>
/// 内部接口：供 Python data-fetcher 调用，触发日终 IV 计算。
///
/// 鉴权：Header <c>X-Internal-Key</c>，值需与 appsettings 中 <c>InternalKey:Key</c> 一致。
/// 幂等：若指定交易日已计算完成，立即返回 status=completed；否则写入 Channel，返回 202。
/// </summary>
[ApiController]
[Route("api/internal")]
public class InternalController(
    Channel<CalcTask> channel,
    IOptions<InternalKeyOptions> keyOptions,
    IOptionRepository repo,
    ILogger<InternalController> logger) : ControllerBase
{
    private static readonly string[] AllUnderlyings = new[] { "510050", "510300", "000300" };

    /// <summary>POST /api/internal/trigger-calc</summary>
    [HttpPost("trigger-calc")]
    public async Task<IActionResult> TriggerCalc(
        [FromBody] TriggerCalcRequest req,
        [FromHeader(Name = "X-Internal-Key")] string? key)
    {
        EnsureAuthorized(key);

        var tradeDate = req.TradeDate.Date;

        // 幂等检查：以 "510050" 为代表查询
        var existing = await repo.GetIvGreeksAsync(tradeDate, "510050");
        if (existing.Count > 0)
        {
            return Ok(new
            {
                code = 200,
                data = new
                {
                    tradeDate = tradeDate.ToString("yyyy-MM-dd"),
                    status = "completed",
                    totalContracts = existing.Count
                },
                message = "该日期已计算完成"
            });
        }

        // 写入 Channel，异步执行
        var sw = Stopwatch.StartNew();
        foreach (var underlying in AllUnderlyings)
        {
            await channel.Writer.WriteAsync(new CalcTask(tradeDate, underlying));
        }

        logger.LogInformation(
            "Calc trigger accepted: tradeDate={TradeDate}, underlyings=3",
            tradeDate.ToString("yyyy-MM-dd"));

        return Accepted(new
        {
            code = 202,
            data = new
            {
                tradeDate = tradeDate.ToString("yyyy-MM-dd"),
                status = "queued",
                elapsedMs = sw.ElapsedMilliseconds
            },
            message = "计算任务已加入队列"
        });
    }

    /// <summary>GET /api/internal/calc-status?tradeDate=2026-07-31</summary>
    [HttpGet("calc-status")]
    public async Task<IActionResult> GetCalcStatus(
        [FromQuery] DateTime tradeDate,
        [FromHeader(Name = "X-Internal-Key")] string? key)
    {
        EnsureAuthorized(key);

        var data = await repo.GetIvGreeksAsync(tradeDate.Date, "510050");
        var status = data.Count > 0 ? "completed" : "queued";

        return Ok(new
        {
            code = 200,
            data = new
            {
                tradeDate = tradeDate.Date.ToString("yyyy-MM-dd"),
                status,
                recordCount = data.Count
            }
        });
    }

    private void EnsureAuthorized(string? key)
    {
        var expected = keyOptions.Value.Key;
        if (string.IsNullOrEmpty(expected))
        {
            logger.LogError("InternalKey is not configured");
            throw new UnauthorizedAccessException("内部密钥未配置");
        }
        if (string.IsNullOrEmpty(key) || key != expected)
        {
            throw new UnauthorizedAccessException("无效的内部密钥");
        }
    }
}