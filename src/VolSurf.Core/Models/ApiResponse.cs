namespace VolSurf.Core.Models;

/// <summary>
/// 统一 API 响应结构（成功 / 错误）。
/// </summary>
/// <typeparam name="T">data 字段承载的数据类型</typeparam>
public class ApiResponse<T>
{
    public int Code { get; set; }
    public T? Data { get; set; }
    public string Message { get; set; } = "success";

    public static ApiResponse<T> Ok(T data, string message = "success") => new()
    {
        Code = 200,
        Data = data,
        Message = message
    };

    public static ApiResponse<T> Accepted(T data, string message = "已接受") => new()
    {
        Code = 202,
        Data = data,
        Message = message
    };
}

/// <summary>统一错误响应。</summary>
public class ApiErrorResponse
{
    public int Code { get; set; }
    public object? Data { get; set; }
    public string Message { get; set; } = "error";
    public ApiErrorDetail Error { get; set; } = default!;
}

public class ApiErrorDetail
{
    public string Type { get; set; } = default!;
    public string Timestamp { get; set; } = default!;
}