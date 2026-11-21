using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace DispatchApi.Infrastructure;

/// <summary>Thrown by services; converted to an RFC 7807 problem response.</summary>
public class ApiException(int statusCode, string title, string? detail = null, object? data = null) : Exception(detail ?? title)
{
    public int StatusCode { get; } = statusCode;
    public string Title { get; } = title;
    public object? Data2 { get; } = data;

    public static ApiException NotFound(string what, object id) => new(404, "Not found", $"{what} {id} was not found.");
    public static ApiException BadRequest(string detail) => new(400, "Bad request", detail);
    public static ApiException Conflict(string detail, object? data = null) => new(409, "Conflict", detail, data);
}

public class ApiExceptionHandler(IProblemDetailsService problemDetails, ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception ex, CancellationToken ct)
    {
        var pd = new ProblemDetails();
        if (ex is ApiException api)
        {
            pd.Status = api.StatusCode;
            pd.Title = api.Title;
            pd.Detail = api.Message;
            if (api.Data2 != null) pd.Extensions["data"] = api.Data2;
        }
        else
        {
            logger.LogError(ex, "Unhandled exception");
            pd.Status = 500;
            pd.Title = "Server error";
            pd.Detail = ex.Message;
        }
        http.Response.StatusCode = pd.Status.Value;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext { HttpContext = http, ProblemDetails = pd, Exception = ex });
    }
}
