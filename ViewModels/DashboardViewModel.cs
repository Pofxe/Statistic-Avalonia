using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using StepikAnalyticsDesktop.Data;
using StepikAnalyticsDesktop.Domain;
using StepikAnalyticsDesktop.Services;
using StepikAnalyticsDesktop.Utils;

namespace StepikAnalyticsDesktop.ViewModels;

public sealed class DashboardViewModel : ViewModelBase
{
    private readonly SqliteDbContextFactory _dbContextFactory;
    private readonly AggregationService _aggregationService;
    private readonly SettingsService _settingsService;
    private readonly UiLogger _logger;
    private DateTimeOffset? _selectedDate;
    private PeriodKind _selectedPeriod;
    private bool _showRawTable;

    public DashboardViewModel(
        SqliteDbContextFactory dbContextFactory,
        AggregationService aggregationService,
        SettingsService settingsService,
        UiLogger logger)
    {
        _dbContextFactory = dbContextFactory;
        _aggregationService = aggregationService;
        _settingsService = settingsService;
        _logger = logger;

        Periods = Enum.GetValues<PeriodKind>().ToList();
        _selectedPeriod = PeriodKind.Month;
        var timeZone = TimeZoneProvider.GetById(_settingsService.TimeZoneId);
        _selectedDate = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, timeZone);

        RefreshCommand = new AsyncCommand(RefreshAsync);
        _ = LoadCoursesAsync(CancellationToken.None);
    }

    public List<PeriodKind> Periods { get; }
    public ObservableCollection<CourseRow> Courses { get; } = new();
    public IList<object> SelectedCourses { get; } = new ObservableCollection<object>();

    public DateTimeOffset? SelectedDate
    {
        get => _selectedDate;
        set => SetProperty(ref _selectedDate, value);
    }

    public PeriodKind SelectedPeriod
    {
        get => _selectedPeriod;
        set => SetProperty(ref _selectedPeriod, value);
    }

    public bool ShowRawTable
    {
        get => _showRawTable;
        set => SetProperty(ref _showRawTable, value);
    }

    public ObservableCollection<MetricCard> MetricCards { get; } = new();
    public ObservableCollection<DailyMetricRow> DailyRows { get; } = new();

    public ISeries[] AttemptsSeries { get; private set; } = Array.Empty<ISeries>();
    public ISeries[] NewStudentsSeries { get; private set; } = Array.Empty<ISeries>();
    public ISeries[] CertificatesSeries { get; private set; } = Array.Empty<ISeries>();
    public ISeries[] RatingSeries { get; private set; } = Array.Empty<ISeries>();
    public ISeries[] ActiveUsersSeries { get; private set; } = Array.Empty<ISeries>();
    public ISeries[] ReviewsDistributionSeries { get; private set; } = Array.Empty<ISeries>();

    public Axis[] TimeAxes { get; private set; } = new[] { new Axis() };
    public Axis[] ReviewsAxes { get; private set; } = new[] { new Axis() };
    public Axis[] ValueAxes { get; private set; } = new[] { new Axis() };

    public string ReviewsAverage { get; private set; } = "недоступно";
    public string ReviewsMedian { get; private set; } = "недоступно";

    public AsyncCommand RefreshCommand { get; }

    private async Task LoadCoursesAsync(CancellationToken cancellationToken)
    {
        await using var context = _dbContextFactory.CreateDbContext();
        var courses = context.Courses.OrderBy(x => x.Title).ToList();
        Courses.Clear();
        foreach (var course in courses)
        {
            Courses.Add(CourseRow.FromEntity(course));
        }
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        if (SelectedDate is null)
        {
            return;
        }

        var selectedCourseIds = SelectedCourses
            .OfType<CourseRow>()
            .Select(x => x.CourseId)
            .ToArray();

        if (selectedCourseIds.Length == 0)
        {
            _logger.Warn("No courses selected for dashboard.");
            return;
        }

        var range = TimeRange.From(DateOnly.FromDateTime(SelectedDate.Value.DateTime), SelectedPeriod);
        var daily = await _aggregationService.GetDailyMetricsAsync(selectedCourseIds, range, cancellationToken);
        var summary = await _aggregationService.GetSummaryAsync(selectedCourseIds, range, cancellationToken);

        MetricCards.Clear();
        MetricCards.Add(new MetricCard("Всего решений", summary.Current.TotalAttempts.ToString(CultureInfo.CurrentCulture),
            Delta(summary.Current.TotalAttempts, summary.Previous.TotalAttempts)));
        MetricCards.Add(new MetricCard("Правильные", summary.Current.CorrectAttempts.ToString(CultureInfo.CurrentCulture),
            Delta(summary.Current.CorrectAttempts, summary.Previous.CorrectAttempts)));
        MetricCards.Add(new MetricCard("Неправильные", summary.Current.WrongAttempts.ToString(CultureInfo.CurrentCulture),
            Delta(summary.Current.WrongAttempts, summary.Previous.WrongAttempts)));
        MetricCards.Add(new MetricCard("Новые ученики", summary.Current.NewStudents.ToString(CultureInfo.CurrentCulture),
            Delta(summary.Current.NewStudents, summary.Previous.NewStudents)));
        MetricCards.Add(new MetricCard("Сертификаты", summary.Current.CertificatesIssued.ToString(CultureInfo.CurrentCulture),
            Delta(summary.Current.CertificatesIssued, summary.Previous.CertificatesIssued)));
        MetricCards.Add(new MetricCard("Отзывы", summary.Current.ReviewsCount.ToString(CultureInfo.CurrentCulture),
            Delta(summary.Current.ReviewsCount, summary.Previous.ReviewsCount)));
        MetricCards.Add(new MetricCard("Активные", summary.Current.ActiveUsers.ToString(CultureInfo.CurrentCulture),
            Delta(summary.Current.ActiveUsers, summary.Previous.ActiveUsers)));
        MetricCards.Add(new MetricCard("Рейтинг курса",
            summary.Current.RatingValue?.ToString("0.##", CultureInfo.CurrentCulture) ?? "недоступно",
            RatingDelta(summary.Current.RatingValue, summary.Previous.RatingValue)));
        MetricCards.Add(new MetricCard("Репутация/знания", "недоступно", "н/д"));

        ReviewsAverage = summary.Current.ReviewsAverage?.ToString("0.##", CultureInfo.CurrentCulture) ?? "недоступно";
        ReviewsMedian = summary.Current.ReviewsMedian?.ToString("0.##", CultureInfo.CurrentCulture) ?? "недоступно";
        RaisePropertyChanged(nameof(ReviewsAverage));
        RaisePropertyChanged(nameof(ReviewsMedian));

        DailyRows.Clear();
        foreach (var row in daily)
        {
            DailyRows.Add(DailyMetricRow.FromEntity(row));
        }

        var labels = daily.Select(x => x.Date.ToString("MM-dd")).ToArray();
        TimeAxes = new[] { new Axis { Labels = labels } };
        ReviewsAxes = new[] { new Axis { Labels = new[] { "1", "2", "3", "4", "5" } } };

        AttemptsSeries = new ISeries[]
        {
            new LineSeries<double> { Values = daily.Select(x => (double)x.TotalAttempts).ToArray(), Name = "Всего" },
            new LineSeries<double> { Values = daily.Select(x => (double)x.CorrectAttempts).ToArray(), Name = "Правильные" },
            new LineSeries<double> { Values = daily.Select(x => (double)x.WrongAttempts).ToArray(), Name = "Неправильные" }
        };
        NewStudentsSeries = new ISeries[]
        {
            new ColumnSeries<double> { Values = daily.Select(x => (double)x.NewStudents).ToArray(), Name = "Новые" }
        };
        CertificatesSeries = new ISeries[]
        {
            new ColumnSeries<double> { Values = daily.Select(x => (double)x.CertificatesIssued).ToArray(), Name = "Сертификаты" }
        };
        RatingSeries = new ISeries[]
        {
            new LineSeries<double> { Values = daily.Select(x => (double)(x.RatingValue ?? 0)).ToArray(), Name = "Рейтинг" }
        };
        ActiveUsersSeries = new ISeries[]
        {
            new ColumnSeries<double> { Values = daily.Select(x => (double)x.ActiveUsers).ToArray(), Name = "Активные" }
        };
        ReviewsDistributionSeries = new ISeries[]
        {
            new ColumnSeries<double>
            {
                Values = new double[]
                {
                    summary.Current.ReviewsStar1,
                    summary.Current.ReviewsStar2,
                    summary.Current.ReviewsStar3,
                    summary.Current.ReviewsStar4,
                    summary.Current.ReviewsStar5
                },
                Name = "Распределение"
            }
        };

        RaisePropertyChanged(nameof(TimeAxes));
        RaisePropertyChanged(nameof(ReviewsAxes));
        RaisePropertyChanged(nameof(AttemptsSeries));
        RaisePropertyChanged(nameof(NewStudentsSeries));
        RaisePropertyChanged(nameof(CertificatesSeries));
        RaisePropertyChanged(nameof(RatingSeries));
        RaisePropertyChanged(nameof(ActiveUsersSeries));
        RaisePropertyChanged(nameof(ReviewsDistributionSeries));
    }

    private static string Delta(int current, int previous)
    {
        var delta = current - previous;
        return delta switch
        {
            > 0 => $"+{delta}",
            < 0 => delta.ToString(CultureInfo.CurrentCulture),
            _ => "0"
        };
    }

    private static string RatingDelta(decimal? current, decimal? previous)
    {
        if (!current.HasValue || !previous.HasValue)
        {
            return "н/д";
        }

        var delta = current.Value - previous.Value;
        return delta switch
        {
            > 0 => $"+{delta:0.##}",
            < 0 => delta.ToString("0.##", CultureInfo.CurrentCulture),
            _ => "0"
        };
    }
}

public sealed record DailyMetricRow(
    string Date,
    int TotalAttempts,
    int CorrectAttempts,
    int WrongAttempts,
    int NewStudents,
    int CertificatesIssued,
    int ActiveUsers)
{
    public static DailyMetricRow FromEntity(DailyMetricEntity entity)
    {
        return new DailyMetricRow(
            entity.Date.ToString("yyyy-MM-dd"),
            entity.TotalAttempts,
            entity.CorrectAttempts,
            entity.WrongAttempts,
            entity.NewStudents,
            entity.CertificatesIssued,
            entity.ActiveUsers);
    }
}
