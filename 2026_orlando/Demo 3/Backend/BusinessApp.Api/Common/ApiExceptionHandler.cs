using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BusinessApp.Api.Common;

/// <summary>Maps well-known exceptions to RFC 7807 problem responses.</summary>
public sealed class ApiExceptionHandler(IProblemDetailsService problemDetails, ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, title, detail) = exception switch
        {
            GridQueryException e => (StatusCodes.Status400BadRequest, "Invalid query", e.Message),
            BadHttpRequestException e => (e.StatusCode, "Bad request", e.Message),
            DbUpdateException { InnerException: SqliteException { SqliteErrorCode: 19 } } =>
                (StatusCodes.Status409Conflict, "Conflict", "The record is referenced by other data or violates a uniqueness rule."),
            _ => (StatusCodes.Status500InternalServerError, "Server error", "An unexpected error occurred."),
        };

        if (status >= 500) logger.LogError(exception, "Unhandled exception");

        context.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            ProblemDetails = { Status = status, Title = title, Detail = detail },
        });
    }
}
