using System;
using System.Collections.Generic;
using StepikAnalyticsDesktop.Domain;

namespace StepikAnalyticsDesktop.Data;

public sealed class CourseEntity
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public DateTimeOffset AddedAt { get; set; }
    public DateTimeOffset? LastSyncAt { get; set; }
    public SyncStatus SyncStatus { get; set; } = SyncStatus.Never;
    public string? LastError { get; set; }
    public DateTimeOffset? LastSyncedEventAt { get; set; }

    public List<SyncRunEntity> SyncRuns { get; set; } = new();
    public List<DailyMetricEntity> DailyMetrics { get; set; } = new();
    public List<AttemptRawEntity> AttemptsRaw { get; set; } = new();
}

public sealed class SyncRunEntity
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public CourseEntity? Course { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public SyncRunStatus Status { get; set; }
    public string? ErrorText { get; set; }
}

public sealed class DailyMetricEntity
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public CourseEntity? Course { get; set; }
    public DateOnly Date { get; set; }
    public int TotalAttempts { get; set; }
    public int CorrectAttempts { get; set; }
    public int WrongAttempts { get; set; }
    public int NewStudents { get; set; }
    public int CertificatesIssued { get; set; }
    public int ReviewsCount { get; set; }
    public int ActiveUsers { get; set; }
    public decimal? RatingValue { get; set; }
    public decimal? ReviewsAverage { get; set; }
    public decimal? ReviewsMedian { get; set; }
    public int? ReviewsStar1 { get; set; }
    public int? ReviewsStar2 { get; set; }
    public int? ReviewsStar3 { get; set; }
    public int? ReviewsStar4 { get; set; }
    public int? ReviewsStar5 { get; set; }
}

public sealed class AttemptRawEntity
{
    public int Id { get; set; }
    public long AttemptId { get; set; }
    public int CourseId { get; set; }
    public CourseEntity? Course { get; set; }
    public long UserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public bool IsCorrect { get; set; }
}
