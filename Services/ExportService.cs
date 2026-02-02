using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CsvHelper;
using Microsoft.EntityFrameworkCore;
using StepikAnalyticsDesktop.Data;
using StepikAnalyticsDesktop.Domain;
using StepikAnalyticsDesktop.Utils;

namespace StepikAnalyticsDesktop.Services;

public sealed class ExportService
{
    private readonly SqliteDbContextFactory _dbContextFactory;
    private readonly SettingsService _settingsService;
    private readonly UiLogger _logger;

    public ExportService(SqliteDbContextFactory dbContextFactory, SettingsService settingsService, UiLogger logger)
    {
        _dbContextFactory = dbContextFactory;
        _settingsService = settingsService;
        _logger = logger;
    }

    public async Task<string?> ExportCsvAsync(int courseId, TimeRange range, CancellationToken cancellationToken)
    {
        await using var context = _dbContextFactory.CreateDbContext();
        var rows = await context.DailyMetrics
            .Where(x => x.CourseId == courseId && x.Date >= range.Start && x.Date <= range.End)
            .OrderBy(x => x.Date)
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            _logger.Warn("No data to export.");
            return null;
        }

        var fileName = $"stepik_course_{courseId}_{range.Period}_{range.Start:yyyyMMdd}_{range.End:yyyyMMdd}.csv";
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "StepikAnalyticsExports");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);

        await using var writer = new StreamWriter(path);
        await using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
        await csv.WriteRecordsAsync(rows, cancellationToken);

        _logger.Info($"Exported CSV to {path}");
        return path;
    }
}
