using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using StepikAnalyticsDesktop.Infrastructure.Auth;
using StepikAnalyticsDesktop.Utils;

namespace StepikAnalyticsDesktop.Infrastructure;

public sealed class StepikApiClient
{
    private readonly HttpClient _httpClient;
    private readonly TokenAuthProvider _authProvider;
    private readonly UiLogger _logger;

    public StepikApiClient(TokenAuthProvider authProvider, UiLogger logger)
    {
        _authProvider = authProvider;
        _logger = logger;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://stepik.org/api/")
        };
    }

    public async Task<ApiResult<IReadOnlyList<AttemptDto>>> GetAttemptsAsync(
        int courseId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        if (!ApiEndpointCatalog.AttemptsEndpoint.HasValue)
        {
            return ApiResult<IReadOnlyList<AttemptDto>>.Unavailable(ApiEndpointCatalog.AttemptsEndpoint.MissingReason);
        }

        var url = $"{ApiEndpointCatalog.AttemptsEndpoint.Value}?course={courseId}&from={from:O}&to={to:O}";
        return await GetPagedAsync<AttemptDto>(url, cancellationToken);
    }

    public async Task<ApiResult<IReadOnlyList<EnrollmentDto>>> GetEnrollmentsAsync(
        int courseId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        if (!ApiEndpointCatalog.EnrollmentsEndpoint.HasValue)
        {
            return ApiResult<IReadOnlyList<EnrollmentDto>>.Unavailable(ApiEndpointCatalog.EnrollmentsEndpoint.MissingReason);
        }

        var url = $"{ApiEndpointCatalog.EnrollmentsEndpoint.Value}?course={courseId}&from={from:O}&to={to:O}";
        return await GetPagedAsync<EnrollmentDto>(url, cancellationToken);
    }

    public async Task<ApiResult<IReadOnlyList<CertificateDto>>> GetCertificatesAsync(
        int courseId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        if (!ApiEndpointCatalog.CertificatesEndpoint.HasValue)
        {
            return ApiResult<IReadOnlyList<CertificateDto>>.Unavailable(ApiEndpointCatalog.CertificatesEndpoint.MissingReason);
        }

        var url = $"{ApiEndpointCatalog.CertificatesEndpoint.Value}?course={courseId}&from={from:O}&to={to:O}";
        return await GetPagedAsync<CertificateDto>(url, cancellationToken);
    }

    public async Task<ApiResult<IReadOnlyList<ReviewDto>>> GetReviewsAsync(
        int courseId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        if (!ApiEndpointCatalog.ReviewsEndpoint.HasValue)
        {
            return ApiResult<IReadOnlyList<ReviewDto>>.Unavailable(ApiEndpointCatalog.ReviewsEndpoint.MissingReason);
        }

        var url = $"{ApiEndpointCatalog.ReviewsEndpoint.Value}?course={courseId}&from={from:O}&to={to:O}";
        return await GetPagedAsync<ReviewDto>(url, cancellationToken);
    }

    public async Task<ApiResult<IReadOnlyList<RatingDto>>> GetRatingsAsync(
        int courseId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        if (!ApiEndpointCatalog.RatingsEndpoint.HasValue)
        {
            return ApiResult<IReadOnlyList<RatingDto>>.Unavailable(ApiEndpointCatalog.RatingsEndpoint.MissingReason);
        }

        var url = $"{ApiEndpointCatalog.RatingsEndpoint.Value}?course={courseId}&from={from:O}&to={to:O}";
        return await GetPagedAsync<RatingDto>(url, cancellationToken);
    }

    private async Task<ApiResult<IReadOnlyList<T>>> GetPagedAsync<T>(string url, CancellationToken cancellationToken)
    {
        return await RetryPolicy.ExecuteAsync(async ct =>
        {
            var results = new List<T>();
            var pageUrl = url;

            while (!string.IsNullOrWhiteSpace(pageUrl))
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, pageUrl);
                _authProvider.Apply(request);
                using var response = await _httpClient.SendAsync(request, ct);

                if ((int)response.StatusCode == 429)
                {
                    _logger.Warn("Rate limit hit. Retrying with backoff.");
                    throw new HttpRequestException("Rate limit", null, response.StatusCode);
                }

                response.EnsureSuccessStatusCode();
                var payload = await response.Content.ReadAsStringAsync(ct);
                var page = JsonSerializer.Deserialize<PagedResponse<T>>(payload, JsonSerializerOptions) ?? new PagedResponse<T>();
                results.AddRange(page.Items);
                pageUrl = page.NextPage;
            }

            return ApiResult<IReadOnlyList<T>>.Available(results);
        }, _logger, cancellationToken: cancellationToken);
    }

    private static JsonSerializerOptions JsonSerializerOptions => new()
    {
        PropertyNameCaseInsensitive = true
    };
}

public sealed record ApiResult<T>(bool IsAvailable, T? Data, string? Reason)
{
    public static ApiResult<T> Available(T data) => new(true, data, null);
    public static ApiResult<T> Unavailable(string reason) => new(false, default, reason);
}

public sealed record ApiEndpoint(string? Value, string MissingReason)
{
    public bool HasValue => !string.IsNullOrWhiteSpace(Value);
}

public static class ApiEndpointCatalog
{
    public static readonly ApiEndpoint AttemptsEndpoint = new(null, "Endpoint for attempts must be уточнен в документации Stepik API.");
    public static readonly ApiEndpoint EnrollmentsEndpoint = new(null, "Endpoint for enrollments must be уточнен в документации Stepik API.");
    public static readonly ApiEndpoint CertificatesEndpoint = new(null, "Endpoint for certificates must be уточнен в документации Stepik API.");
    public static readonly ApiEndpoint ReviewsEndpoint = new(null, "Endpoint for reviews must be уточнен в документации Stepik API.");
    public static readonly ApiEndpoint RatingsEndpoint = new(null, "Endpoint for rating history must be уточнен в документации Stepik API.");
}

public sealed class PagedResponse<T>
{
    public List<T> Items { get; set; } = new();
    public string? NextPage { get; set; }
}

public sealed record AttemptDto(long AttemptId, int CourseId, long UserId, DateTimeOffset CreatedAt, bool IsCorrect);
public sealed record EnrollmentDto(long UserId, DateTimeOffset CreatedAt);
public sealed record CertificateDto(long UserId, DateTimeOffset IssuedAt);
public sealed record ReviewDto(long ReviewId, int Stars, DateTimeOffset CreatedAt);
public sealed record RatingDto(decimal Rating, DateTimeOffset RecordedAt);
