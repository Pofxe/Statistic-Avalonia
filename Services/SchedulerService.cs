using System;
using System.Threading;
using System.Threading.Tasks;
using StepikAnalyticsDesktop.Utils;

namespace StepikAnalyticsDesktop.Services;

public sealed class SchedulerService
{
    private readonly SettingsService _settingsService;
    private readonly SyncService _syncService;
    private readonly UiLogger _logger;
    private Timer? _timer;

    public SchedulerService(SettingsService settingsService, SyncService syncService, UiLogger logger)
    {
        _settingsService = settingsService;
        _syncService = syncService;
        _logger = logger;
        ConfigureTimer();
    }

    public void ConfigureTimer()
    {
        _timer?.Dispose();

        if (!_settingsService.AutoSyncEnabled)
        {
            return;
        }

        _timer = new Timer(async _ => await RunAsync(), null, TimeSpan.FromMinutes(1), TimeSpan.FromHours(24));
    }

    private async Task RunAsync()
    {
        try
        {
            _logger.Info("Auto sync started.");
            await _syncService.SyncAllAsync(CancellationToken.None);
            _logger.Info("Auto sync finished.");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Auto sync failed.");
        }
    }
}
