using System;

namespace StepikAnalyticsDesktop.Domain;

public sealed record DailyMetric(
    int CourseId,
    DateOnly Date,
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
    int? ReviewsStar1,
    int? ReviewsStar2,
    int? ReviewsStar3,
    int? ReviewsStar4,
    int? ReviewsStar5);

public sealed record MetricCard(string Title, string Value, string Delta);
