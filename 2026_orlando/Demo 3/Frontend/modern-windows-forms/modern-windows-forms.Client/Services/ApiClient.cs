using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using modern_windows_forms.Client.Models;

namespace modern_windows_forms.Client.Services;

/// <summary>
/// Thin typed wrapper over the BusinessApp Web API (http://localhost:5080).
/// Non-success responses are surfaced as <see cref="ApiException"/> carrying the RFC 7807 ProblemDetails body.
/// </summary>
public sealed class ApiClient(HttpClient http, ILogger<ApiClient> logger)
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public Uri BaseAddress => http.BaseAddress!;

    /// <summary>Turns an API-relative asset path such as <c>/api/avatars/customers/1.svg</c> into an absolute URL.</summary>
    public string AssetUrl(string? path) =>
        string.IsNullOrEmpty(path) ? string.Empty : new Uri(BaseAddress, path).ToString();

    public async Task<T> GetAsync<T>(string url, CancellationToken ct = default)
    {
        using var response = await SendAsync(new HttpRequestMessage(HttpMethod.Get, url), ct);
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct))!;
    }

    public Task<TResult> PostAsync<TResult>(string url, object? body = null, CancellationToken ct = default) =>
        SendJsonAsync<TResult>(HttpMethod.Post, url, body, ct);

    public Task<TResult> PutAsync<TResult>(string url, object? body, CancellationToken ct = default) =>
        SendJsonAsync<TResult>(HttpMethod.Put, url, body, ct);

    public Task<TResult> PatchAsync<TResult>(string url, object? body, CancellationToken ct = default) =>
        SendJsonAsync<TResult>(HttpMethod.Patch, url, body, ct);

    public async Task SendAsync(HttpMethod method, string url, object? body = null, CancellationToken ct = default)
    {
        var request = new HttpRequestMessage(method, url);
        if (body is not null) request.Content = JsonContent.Create(body, options: JsonOptions);
        using var _ = await SendAsync(request, ct);
    }

    private async Task<TResult> SendJsonAsync<TResult>(HttpMethod method, string url, object? body, CancellationToken ct)
    {
        var request = new HttpRequestMessage(method, url);
        if (body is not null) request.Content = JsonContent.Create(body, options: JsonOptions);
        using var response = await SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.NoContent || response.Content.Headers.ContentLength == 0)
            return default!;
        return (await response.Content.ReadFromJsonAsync<TResult>(JsonOptions, ct))!;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, ct);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Network error calling {Method} {Url}", request.Method, request.RequestUri);
            throw new ApiException(new ProblemDetails
            {
                Title = "Can't reach the server",
                Detail = $"The BusinessApp API at {BaseAddress} isn't responding. Check that it is running, then retry.",
                Status = 0
            });
        }

        if (response.IsSuccessStatusCode) return response;

        ProblemDetails? problem = null;
        try
        {
            problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions, ct);
        }
        catch (JsonException) { /* non-JSON error body */ }

        problem ??= new ProblemDetails { Title = response.ReasonPhrase ?? "Request failed" };
        problem.Status ??= (int)response.StatusCode;
        logger.LogInformation("API {Status} for {Method} {Url}: {Title}", problem.Status, request.Method, request.RequestUri, problem.Title);
        response.Dispose();
        throw new ApiException(problem);
    }
}

/// <summary>An API call failed; <see cref="Problem"/> holds the RFC 7807 payload (400/404/409, or Status 0 for network errors).</summary>
public sealed class ApiException(ProblemDetails problem) : Exception(problem.Title)
{
    public ProblemDetails Problem { get; } = problem;
    public int Status => Problem.Status ?? 0;
    public bool IsNetworkError => Status == 0;
}
