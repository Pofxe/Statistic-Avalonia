using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace StepikAnalyticsDesktop.Utils;

public static class RetryPolicy
{
    public static async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> action,
        UiLogger logger,
        int maxRetries = 5,
        int baseDelayMs = 500,
        CancellationToken cancellationToken = default)
    {
        var jitter = new Random();
        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                return await action(cancellationToken);
            }
            catch (HttpRequestException ex) when (IsTransient(ex))
            {
                var delay = TimeSpan.FromMilliseconds(baseDelayMs * Math.Pow(2, attempt - 1))
                    + TimeSpan.FromMilliseconds(jitter.Next(0, 250));
                logger.Warn($"Transient error: {ex.Message}. Retrying in {delay.TotalMilliseconds:N0} ms.");
                await Task.Delay(delay, cancellationToken);
            }
        }

        return await action(cancellationToken);
    }

    private static bool IsTransient(HttpRequestException ex)
    {
        if (ex.StatusCode is null)
        {
            return true;
        }

        return ex.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.RequestTimeout
            or HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout;
    }
}
