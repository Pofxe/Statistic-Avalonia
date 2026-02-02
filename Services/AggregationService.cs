using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using StepikAnalyticsDesktop.Data;
using StepikAnalyticsDesktop.Domain;

namespace StepikAnalyticsDesktop.Services;

public sealed class AggregationService
{
    private readonly SqliteDbContextFactory _dbContextFactory;
    private readonly UiLogger _logger;

    public AggregationService(SqliteDbContextFactory dbContextFactory, UiLogger logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public async Task<IReadOnlyList<DailyMetricEntity>> GetDailyMetricsAsync(
        IReadOnlyCollection<int> courseIds,
        TimeRange range,
        CancellationToken cancellationToken)
    {
        if (courseIds.Count == 0)
        {
            return Array.Empty<DailyMetricEntity>();
        }

        await using var context = _dbContextFactory.CreateDbContext();
        return await context.DailyMetrics
            .Where(x => courseIds.Contains(x.CourseId) && x.Date >= range.Start && x.Date <= range.End)
            .OrderBy(x => x.Date)
            .ToListAsync(cancellationToken);
    }

    public async Task<AggregatedSummary> GetSummaryAsync(
        IReadOnlyCollection<int> courseIds,
        TimeRange range,
        CancellationToken cancellationToken)
    {
        var current = await GetDailyMetricsAsync(courseIds, range, cancellationToken);
        var previous = await GetDailyMetricsAsync(courseIds, range.Previous(), cancellationToken);

        var currentSummary = Aggregate(current);
        var previousSummary = Aggregate(previous);

        return new AggregatedSummary(currentSummary, previousSummary);
    }

    private static Summary Aggregate(IReadOnlyList<DailyMetricEntity> items)
    {
        return new Summary(
            items.Sum(x => x.TotalAttempts),
            items.Sum(x => x.CorrectAttempts),
            items.Sum(x => x.WrongAttempts),
            items.Sum(x => x.NewStudents),
            items.Sum(x => x.CertificatesIssued),
            items.Sum(x => x.ReviewsCount),
            items.Sum(x => x.ActiveUsers),
            items.OrderByDescending(x => x.Date).FirstOrDefault()?.RatingValue,
            AverageNullable(items.Select(x => x.ReviewsAverage)),
            MedianNullable(items.Select(x => x.ReviewsMedian)),
            items.Sum(x => x.ReviewsStar1 ?? 0),
            items.Sum(x => x.ReviewsStar2 ?? 0),
            items.Sum(x => x.ReviewsStar3 ?? 0),
            items.Sum(x => x.ReviewsStar4 ?? 0),
            items.Sum(x => x.ReviewsStar5 ?? 0));
    }

    private static decimal? AverageNullable(IEnumerable<decimal?> values)
    {
        var valid = values.Where(x => x.HasValue).Select(x => x.Value).ToList();
        if (valid.Count == 0)
        {
            return null;
        }

        return valid.Average();
    }

    private static decimal? MedianNullable(IEnumerable<decimal?> values)
    {
        var ordered = values.Where(x => x.HasValue).Select(x => x.Value).OrderBy(x => x).ToArray();
        if (ordered.Length == 0)
        {
            return null;
        }

        return ordered.Length % 2 == 1
            ? ordered[ordered.Length / 2]
            : (ordered[ordered.Length / 2 - 1] + ordered[ordered.Length / 2]) / 2;
    }
}

public sealed record Summary(
    int TotalAttempts,
    int CorrectAttempts,
    int WrongAttempts,
    int NewStudents,
    int CertificatesIssued,
    int ReviewsCount,
    int ActiveUsers,
    decimal? RatingValue,
    decimal? ReviewsAverage,
    decimal? ReviewsMedian,
    int ReviewsStar1,
    int ReviewsStar2,
    int ReviewsStar3,
    int ReviewsStar4,
    int ReviewsStar5);

public sealed record AggregatedSummary(Summary Current, Summary Previous);
