using System.Net;
using System.Text.Json;
using FluentValidation;
using VolSurf.Core.Models;

namespace VolSurf.Api.Middleware;

/// <summary>
/// 全局异常处理中间件。
///
/// 异常分类：
///   - ValidationException (FluentValidation) -> 400 VALIDATION_ERROR
///   - ArgumentException                   -> 400 BAD_REQUEST
///   - KeyNotFoundException                -> 404 NOT_FOUND
///   - UnauthorizedAccessException         -> 401 UNAUTHORIZED
///   - 其它                                -> 500 INTERNAL_ERROR
///
/// 响应统一格式：{ code, data, message, error: { type, timestamp } }
/// </summary>
public class GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await next(ctx);
        }
        catch (ValidationException ex)
        {
            await WriteError(ctx, (int)HttpStatusCode.BadRequest, "VALIDATION_ERROR",
                string.IsNullOrWhiteSpace(ex.Message) ? "参数校验失败" : ex.Message);
        }
        catch (ArgumentException ex)
        {
            await WriteError(ctx, (int)HttpStatusCode.BadRequest, "BAD_REQUEST",
                ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            await WriteError(ctx, (int)HttpStatusCode.NotFound, "NOT_FOUND",
                ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            await WriteError(ctx, (int)HttpStatusCode.Unauthorized, "UNAUTHORIZED",
                ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception on {Method} {Path}",
                ctx.Request.Method, ctx.Request.Path);
            await WriteError(ctx, (int)HttpStatusCode.InternalServerError,
                "INTERNAL_ERROR", "服务内部错误");
        }
    }

    private static async Task WriteError(HttpContext ctx, int statusCode, string errorType, string message)
    {
        if (ctx.Response.HasStarted)
        {
            // 响应已开始写入，无法再设置 Header / 状态码
            return;
        }

        ctx.Response.Clear();
        ctx.Response.StatusCode = statusCode;
        ctx.Response.ContentType = "application/json; charset=utf-8";

        var response = new ApiErrorResponse
        {
            Code = statusCode,
            Data = null,
            Message = message,
            Error = new ApiErrorDetail
            {
                Type = errorType,
                Timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
            }
        };

        await ctx.Response.WriteAsync(JsonSerializer.Serialize(response, JsonOptions));
    }
}