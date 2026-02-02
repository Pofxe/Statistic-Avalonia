using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using StepikAnalyticsDesktop.Data;
using StepikAnalyticsDesktop.Services;
using StepikAnalyticsDesktop.Utils;
using StepikAnalyticsDesktop.ViewModels;
using StepikAnalyticsDesktop.Views;

namespace StepikAnalyticsDesktop.App;

public sealed class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var logger = new UiLogger();
            var settingsService = new SettingsService(logger);
            var dbContextFactory = new SqliteDbContextFactory(settingsService, logger);
            var apiClient = new StepikAnalyticsDesktop.Infrastructure.StepikApiClient(
                new StepikAnalyticsDesktop.Infrastructure.Auth.TokenAuthProvider(settingsService),
                logger);
            var syncService = new SyncService(dbContextFactory, apiClient, settingsService, logger);
            var aggregationService = new AggregationService(dbContextFactory, logger);
            var mainViewModel = new MainWindowViewModel(
                new DashboardViewModel(dbContextFactory, aggregationService, settingsService, logger),
                new CoursesViewModel(dbContextFactory, syncService, settingsService, logger));

            desktop.MainWindow = new MainWindow { DataContext = mainViewModel };

            _ = dbContextFactory.ApplyMigrationsAsync(default);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
