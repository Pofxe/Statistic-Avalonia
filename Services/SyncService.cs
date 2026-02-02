using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using StepikAnalyticsDesktop.Data;
using StepikAnalyticsDesktop.Domain;
using StepikAnalyticsDesktop.Infrastructure;
using StepikAnalyticsDesktop.Utils;

namespace StepikAnalyticsDesktop.Services;

public sealed class SyncService
{
    private readonly SqliteDbContextFactory _dbContextFactory;
    private readonly StepikApiClient _apiClient;
    private readonly SettingsService _settingsService;
    private readonly UiLogger _logger;
    private CancellationTokenSource? _syncCts;

    public SyncService(
        SqliteDbContextFactory dbContextFactory,
        StepikApiClient apiClient,
        SettingsService settingsService,
        UiLogger logger)
    {
        _dbContextFactory = dbContextFactory;
        _apiClient = apiClient;
        _settingsService = settingsService;
        _logger = logger;
    }

    public bool IsSyncing => _syncCts is { IsCancellationRequested: false };

    public async Task SyncAllAsync(CancellationToken cancellationToken)
    {
        await using var context = _dbContextFactory.CreateDbContext();
        var courseIds = await context.Courses.Select(x => x.CourseId).ToListAsync(cancellationToken);
        foreach (var courseId in courseIds)
        {
            await SyncCourseAsync(courseId, cancellationToken);
        }
    }

    public async Task SyncCourseAsync(int courseId, CancellationToken cancellationToken)
    {
        await using var context = _dbContextFactory.CreateDbContext();
        var course = await context.Courses.FirstOrDefaultAsync(x => x.CourseId == courseId, cancellationToken);
        if (course is null)
        {
            _logger.Warn($"Course {courseId} not found for sync.");
            return;
        }

        _syncCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = _syncCts.Token;

        var run = new SyncRunEntity
        {
            CourseId = course.Id,
            StartedAt = DateTimeOffset.UtcNow,
            Status = SyncRunStatus.Ok
        };
        context.SyncRuns.Add(run);

        course.SyncStatus = SyncStatus.Syncing;
        course.LastError = null;
        await context.SaveChangesAsync(token);

        try
        {
            var backfillDays = _settingsService.BackfillDays;
            var timeZone = TimeZoneProvider.GetById(_settingsService.TimeZoneId);
            var fromDate = course.LastSyncedEventAt.HasValue
                ? TimeZoneProvider.ToLocalDate(course.LastSyncedEventAt.Value, timeZone)
                : DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-backfillDays));
            var from = TimeZoneProvider.GetUtcRange(fromDate, timeZone).StartUtc;
            var to = DateTimeOffset.UtcNow;

            _logger.Info($"Sync course {course.CourseId} from {from:O} to {to:O}.");

            var attempts = await _apiClient.GetAttemptsAsync(course.CourseId, from, to, token);
            if (attempts.IsAvailable && attempts.Data is not null)
            {
                var newAttempts = await UpsertAttemptsAsync(context, course.Id, attempts.Data, token);
                await UpsertAttemptAggregatesAsync(context, course.Id, newAttempts, timeZone, token);
            }
            else
            {
                _logger.Warn($"Attempts unavailable: {attempts.Reason}");
            }

            var enrollments = await _apiClient.GetEnrollmentsAsync(course.CourseId, from, to, token);
            if (enrollments.IsAvailable && enrollments.Data is not null)
            {
                await UpsertDailyCountsAsync(context, course.Id, enrollments.Data.Select(x => x.CreatedAt), timeZone,
                    (metric, count) => metric.NewStudents = count, token);
            }
            else
            {
                _logger.Warn($"Enrollments unavailable: {enrollments.Reason}");
            }

            var certificates = await _apiClient.GetCertificatesAsync(course.CourseId, from, to, token);
            if (certificates.IsAvailable && certificates.Data is not null)
            {
                await UpsertDailyCountsAsync(context, course.Id, certificates.Data.Select(x => x.IssuedAt), timeZone,
                    (metric, count) => metric.CertificatesIssued = count, token);
            }
            else
            {
                _logger.Warn($"Certificates unavailable: {certificates.Reason}");
            }

            var reviews = await _apiClient.GetReviewsAsync(course.CourseId, from, to, token);
            if (reviews.IsAvailable && reviews.Data is not null)
            {
                await UpsertReviewsAsync(context, course.Id, reviews.Data, timeZone, token);
            }
            else
            {
                _logger.Warn($"Reviews unavailable: {reviews.Reason}");
            }

            var ratings = await _apiClient.GetRatingsAsync(course.CourseId, from, to, token);
            if (ratings.IsAvailable && ratings.Data is not null)
            {
                await UpsertRatingsAsync(context, course.Id, ratings.Data, timeZone, token);
            }
            else
            {
                _logger.Warn($"Ratings unavailable: {ratings.Reason}");
            }

            course.LastSyncedEventAt = to;
            course.LastSyncAt = DateTimeOffset.UtcNow;
            course.SyncStatus = SyncStatus.Ok;
            run.FinishedAt = DateTimeOffset.UtcNow;
            run.Status = SyncRunStatus.Ok;
        }
        catch (OperationCanceledException)
        {
            course.SyncStatus = SyncStatus.Error;
            course.LastError = "Sync cancelled";
            run.Status = SyncRunStatus.Cancelled;
            _logger.Warn("Sync cancelled.");
        }
        catch (Exception ex)
        {
            course.SyncStatus = SyncStatus.Error;
            course.LastError = ex.Message;
            run.Status = SyncRunStatus.Error;
            run.ErrorText = ex.Message;
            _logger.Error(ex, "Sync failed.");
        }
        finally
        {
            run.FinishedAt ??= DateTimeOffset.UtcNow;
            await context.SaveChangesAsync(CancellationToken.None);
        }
    }

    public void Cancel()
    {
        _syncCts?.Cancel();
    }

    private static async Task<List<AttemptRawEntity>> UpsertAttemptsAsync(
        AppDbContext context,
        int courseId,
        IReadOnlyList<AttemptDto> attempts,
        CancellationToken cancellationToken)
    {
        if (attempts.Count == 0)
        {
            return new List<AttemptRawEntity>();
        }

        var existingIds = await context.AttemptsRaw
            .Where(x => x.CourseId == courseId)
            .Select(x => x.AttemptId)
            .ToListAsync(cancellationToken);
        var existingSet = existingIds.ToHashSet();

        var newItems = attempts
            .Where(x => !existingSet.Contains(x.AttemptId))
            .Select(x => new AttemptRawEntity
            {
                AttemptId = x.AttemptId,
                CourseId = courseId,
                UserId = x.UserId,
                CreatedAt = x.CreatedAt,
                IsCorrect = x.IsCorrect
            })
            .ToList();

        context.AttemptsRaw.AddRange(newItems);

        await context.SaveChangesAsync(cancellationToken);
        return newItems;
    }

    private static async Task UpsertDailyCountsAsync(
        AppDbContext context,
        int courseId,
        IEnumerable<DateTimeOffset> timestamps,
        TimeZoneInfo timeZone,
        Action<DailyMetricEntity, int> update,
        CancellationToken cancellationToken)
    {
        var grouped = timestamps
            .GroupBy(x => TimeZoneProvider.ToLocalDate(x, timeZone))
            .ToDictionary(x => x.Key, x => x.Count());

        foreach (var (date, count) in grouped)
        {
            var metric = await context.DailyMetrics
                .FirstOrDefaultAsync(x => x.CourseId == courseId && x.Date == date, cancellationToken)
                ?? new DailyMetricEntity { CourseId = courseId, Date = date };

            update(metric, count);

            if (metric.Id == 0)
            {
                context.DailyMetrics.Add(metric);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task UpsertAttemptAggregatesAsync(
        AppDbContext context,
        int courseId,
        IReadOnlyList<AttemptRawEntity> attempts,
        TimeZoneInfo timeZone,
        CancellationToken cancellationToken)
    {
        if (attempts.Count == 0)
        {
            return;
        }

        var grouped = attempts
            .GroupBy(x => TimeZoneProvider.ToLocalDate(x.CreatedAt, timeZone));

        foreach (var group in grouped)
        {
            var metric = await context.DailyMetrics
                .FirstOrDefaultAsync(x => x.CourseId == courseId && x.Date == group.Key, cancellationToken)
                ?? new DailyMetricEntity { CourseId = courseId, Date = group.Key };

            metric.TotalAttempts += group.Count();
            metric.CorrectAttempts += group.Count(x => x.IsCorrect);
            metric.WrongAttempts += group.Count(x => !x.IsCorrect);
            var (startUtc, endUtc) = TimeZoneProvider.GetUtcRange(group.Key, timeZone);
            metric.ActiveUsers = await context.AttemptsRaw
                .Where(x => x.CourseId == courseId && x.CreatedAt >= startUtc && x.CreatedAt < endUtc)
                .Select(x => x.UserId)
                .Distinct()
                .CountAsync(cancellationToken);

            if (metric.Id == 0)
            {
                context.DailyMetrics.Add(metric);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task UpsertReviewsAsync(
        AppDbContext context,
        int courseId,
        IReadOnlyList<ReviewDto> reviews,
        TimeZoneInfo timeZone,
        CancellationToken cancellationToken)
    {
        var grouped = reviews.GroupBy(x => TimeZoneProvider.ToLocalDate(x.CreatedAt, timeZone));
        foreach (var dayGroup in grouped)
        {
            var metric = await context.DailyMetrics
                .FirstOrDefaultAsync(x => x.CourseId == courseId && x.Date == dayGroup.Key, cancellationToken)
                ?? new DailyMetricEntity { CourseId = courseId, Date = dayGroup.Key };

            metric.ReviewsCount = dayGroup.Count();
            metric.ReviewsStar1 = dayGroup.Count(x => x.Stars == 1);
            metric.ReviewsStar2 = dayGroup.Count(x => x.Stars == 2);
            metric.ReviewsStar3 = dayGroup.Count(x => x.Stars == 3);
            metric.ReviewsStar4 = dayGroup.Count(x => x.Stars == 4);
            metric.ReviewsStar5 = dayGroup.Count(x => x.Stars == 5);
            metric.ReviewsAverage = dayGroup.Average(x => (decimal)x.Stars);
            metric.ReviewsMedian = Median(dayGroup.Select(x => (decimal)x.Stars));

            if (metric.Id == 0)
            {
                context.DailyMetrics.Add(metric);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task UpsertRatingsAsync(
        AppDbContext context,
        int courseId,
        IReadOnlyList<RatingDto> ratings,
        TimeZoneInfo timeZone,
        CancellationToken cancellationToken)
    {
        var grouped = ratings.GroupBy(x => TimeZoneProvider.ToLocalDate(x.RecordedAt, timeZone));
        foreach (var dayGroup in grouped)
        {
            var metric = await context.DailyMetrics
                .FirstOrDefaultAsync(x => x.CourseId == courseId && x.Date == dayGroup.Key, cancellationToken)
                ?? new DailyMetricEntity { CourseId = courseId, Date = dayGroup.Key };

            metric.RatingValue = dayGroup.Last().Rating;

            if (metric.Id == 0)
            {
                context.DailyMetrics.Add(metric);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static decimal Median(IEnumerable<decimal> values)
    {
        var ordered = values.OrderBy(x => x).ToArray();
        if (ordered.Length == 0)
        {
            return 0;
        }

        return ordered.Length % 2 == 1
            ? ordered[ordered.Length / 2]
            : (ordered[ordered.Length / 2 - 1] + ordered[ordered.Length / 2]) / 2;
    }
}
