using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using StepikAnalyticsDesktop.Data;
using StepikAnalyticsDesktop.Domain;
using StepikAnalyticsDesktop.Services;
using StepikAnalyticsDesktop.Utils;

namespace StepikAnalyticsDesktop.Tests;

public static class SelfTestRunner
{
    public static async Task<int> RunAsync()
    {
        try
        {
            TestTimeRange();
            await TestAggregationServiceAsync();
            Console.WriteLine("Self-tests passed.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Self-tests failed: {ex.Message}");
            return 1;
        }
    }

    private static void TestTimeRange()
    {
        var date = new DateOnly(2024, 12, 18);
        var week = TimeRange.From(date, PeriodKind.Week);
        if (week.Start.DayOfWeek != DayOfWeek.Monday)
        {
            throw new InvalidOperationException("Week start must be Monday.");
        }

        var month = TimeRange.From(date, PeriodKind.Month);
        if (month.Start.Day != 1 || month.End.Day < 28)
        {
            throw new InvalidOperationException("Month range incorrect.");
        }

        var previous = month.Previous();
        if (previous.End >= month.Start)
        {
            throw new InvalidOperationException("Previous period should be before current.");
        }
    }

    private static async Task TestAggregationServiceAsync()
    {
        var logger = new UiLogger();
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        context.DailyMetrics.AddRange(
            new DailyMetricEntity { CourseId = 1, Date = new DateOnly(2024, 12, 1), TotalAttempts = 10, CorrectAttempts = 7, WrongAttempts = 3, NewStudents = 2, CertificatesIssued = 1, ReviewsCount = 1, ActiveUsers = 5 },
            new DailyMetricEntity { CourseId = 1, Date = new DateOnly(2024, 12, 2), TotalAttempts = 20, CorrectAttempts = 12, WrongAttempts = 8, NewStudents = 3, CertificatesIssued = 2, ReviewsCount = 2, ActiveUsers = 7 });
        await context.SaveChangesAsync();

        var factory = new TestDbContextFactory(options, logger);
        var service = new AggregationService(factory, logger);

        var range = new TimeRange(new DateOnly(2024, 12, 1), new DateOnly(2024, 12, 2), PeriodKind.Day);
        var summary = await service.GetSummaryAsync(new[] { 1 }, range, CancellationToken.None);

        if (summary.Current.TotalAttempts != 30 || summary.Current.NewStudents != 5)
        {
            throw new InvalidOperationException("Aggregation totals incorrect.");
        }
    }
}

internal sealed class TestDbContextFactory : SqliteDbContextFactory
{
    private readonly DbContextOptions<AppDbContext> _options;

    public TestDbContextFactory(DbContextOptions<AppDbContext> options, UiLogger logger)
        : base(new SettingsService(logger), logger)
    {
        _options = options;
    }

    public override AppDbContext CreateDbContext()
    {
        return new AppDbContext(_options);
    }
}
