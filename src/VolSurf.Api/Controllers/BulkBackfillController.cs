using System.Threading.Channels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using VolSurf.Api.BackgroundServices;
using VolSurf.Core.Options;

namespace VolSurf.Api.Controllers;

/// <summary>
/// 批量回算接口：单标的，全交易日遍历，异步消费 Channel 完成回算。
/// 鉴权同 InternalController（X-Internal-Key 头）。
/// </summary>
[ApiController]
[Route("api/internal")]
public class BulkBackfillController(
    Channel<BulkBackfillRequest> channel,
    IOptions<InternalKeyOptions> keyOptions,
    ILogger<BulkBackfillController> logger) : ControllerBase
{
    private static readonly HashSet<string> AllowedUnderlyings =
        new(StringComparer.Ordinal) { "510050", "510300", "000300" };

    /// <summary>POST /api/internal/bulk-backfill  body: {"underlying":"510050"}</summary>
    [HttpPost("bulk-backfill")]
    public async Task<IActionResult> TriggerBulkBackfill(
        [FromBody] BulkBackfillRequest req,
        [FromHeader(Name = "X-Internal-Key")] string? key)
    {
        EnsureAuthorized(key);

        if (req == null || string.IsNullOrWhiteSpace(req.Underlying))
            return BadRequest(new { code = 400, message = "underlying 必填" });

        if (!AllowedUnderlyings.Contains(req.Underlying))
            return BadRequest(new
            {
                code = 400,
                message = $"不支持的标的 {req.Underlying}; allowed: {string.Join(",", AllowedUnderlyings)}"
            });

        await channel.Writer.WriteAsync(req);

        logger.LogInformation(
            "BulkBackfill trigger accepted: underlying={Underlying}", req.Underlying);

        return Accepted(new
        {
            code = 202,
            data = new { underlying = req.Underlying, status = "queued" },
            message = "批量回算已加入队列"
        });
    }

    /// <summary>GET /api/internal/bulk-backfill/status</summary>
    [HttpGet("bulk-backfill/status")]
    public IActionResult GetStatus(
        [FromHeader(Name = "X-Internal-Key")] string? key)
    {
        EnsureAuthorized(key);
        return Ok(new
        {
            code = 200,
            data = new
            {
                channelCount = channel.Reader.Count,
                isCompleted = channel.Reader.Completion.IsCompleted
            }
        });
    }

    private void EnsureAuthorized(string? key)
    {
        var expected = keyOptions.Value.Key;
        if (string.IsNullOrEmpty(expected))
            throw new UnauthorizedAccessException("内部密钥未配置");
        if (string.IsNullOrEmpty(key) || key != expected)
            throw new UnauthorizedAccessException("无效的内部密钥");
    }
}
