using System.Net.Http.Headers;
using StepikAnalyticsDesktop.Services;

namespace StepikAnalyticsDesktop.Infrastructure.Auth;

public sealed class TokenAuthProvider
{
    private readonly SettingsService _settingsService;

    public TokenAuthProvider(SettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public void Apply(HttpRequestMessage request)
    {
        if (!string.IsNullOrWhiteSpace(_settingsService.ApiToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settingsService.ApiToken);
        }
    }
}
