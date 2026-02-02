using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using StepikAnalyticsDesktop.Services;
using StepikAnalyticsDesktop.Utils;

namespace StepikAnalyticsDesktop.Data;

public class SqliteDbContextFactory
{
    private readonly SettingsService _settingsService;
    private readonly UiLogger _logger;

    public SqliteDbContextFactory(SettingsService settingsService, UiLogger logger)
    {
        _settingsService = settingsService;
        _logger = logger;
    }

    public virtual AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_settingsService.DatabasePath}")
            .Options;
        return new AppDbContext(options);
    }

    public async Task ApplyMigrationsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var context = CreateDbContext();
            await context.Database.MigrateAsync(cancellationToken);
            _logger.Info("Database migrations applied.");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Database migration failed.");
        }
    }
}
