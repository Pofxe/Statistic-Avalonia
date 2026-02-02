using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using StepikAnalyticsDesktop.Data;
using StepikAnalyticsDesktop.Domain;
using StepikAnalyticsDesktop.Services;
using StepikAnalyticsDesktop.Utils;

namespace StepikAnalyticsDesktop.ViewModels;

public sealed class SettingsViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService;
    private readonly ExportService _exportService;
    private readonly SchedulerService _schedulerService;
    private readonly UiLogger _logger;
    private readonly SqliteDbContextFactory _dbContextFactory;

    private string _apiToken = string.Empty;
    private int _backfillDays;
    private bool _autoSyncEnabled;
    private TimeZoneInfo _selectedTimeZone;
    private CourseRow? _selectedCourse;
    private PeriodKind _exportPeriod;
    private DateTimeOffset? _exportDate;

    public SettingsViewModel(
        SettingsService settingsService,
        ExportService exportService,
        SchedulerService schedulerService,
        UiLogger logger)
    {
        _settingsService = settingsService;
        _exportService = exportService;
        _schedulerService = schedulerService;
        _logger = logger;
        _dbContextFactory = new SqliteDbContextFactory(settingsService, logger);

        ApiToken = settingsService.ApiToken;
        BackfillDays = settingsService.BackfillDays;
        AutoSyncEnabled = settingsService.AutoSyncEnabled;

        TimeZones = TimeZoneProvider.GetTimeZones();
        _selectedTimeZone = TimeZones.FirstOrDefault(x => x.Id == settingsService.TimeZoneId)
            ?? TimeZoneInfo.Local;

        Periods = Enum.GetValues<PeriodKind>().ToList();
        _exportPeriod = PeriodKind.Month;
        _exportDate = DateTimeOffset.Now;

        SaveTokenCommand = new RelayCommand(_ => SaveToken());
        ExportCsvCommand = new AsyncCommand(ExportCsvAsync);
        ClearCourseCommand = new AsyncCommand(ClearCourseAsync);

        _ = LoadCoursesAsync(CancellationToken.None);
    }

    public string ApiToken
    {
        get => _apiToken;
        set => SetProperty(ref _apiToken, value);
    }

    public int BackfillDays
    {
        get => _backfillDays;
        set
        {
            if (SetProperty(ref _backfillDays, value))
            {
                _settingsService.BackfillDays = value;
            }
        }
    }

    public bool AutoSyncEnabled
    {
        get => _autoSyncEnabled;
        set
        {
            if (SetProperty(ref _autoSyncEnabled, value))
            {
                _settingsService.AutoSyncEnabled = value;
                _schedulerService.ConfigureTimer();
            }
        }
    }

    public IReadOnlyList<TimeZoneInfo> TimeZones { get; }

    public TimeZoneInfo SelectedTimeZone
    {
        get => _selectedTimeZone;
        set
        {
            if (SetProperty(ref _selectedTimeZone, value))
            {
                _settingsService.TimeZoneId = value.Id;
            }
        }
    }

    public ObservableCollection<CourseRow> Courses { get; } = new();

    public CourseRow? SelectedCourse
    {
        get => _selectedCourse;
        set => SetProperty(ref _selectedCourse, value);
    }

    public List<PeriodKind> Periods { get; }

    public PeriodKind ExportPeriod
    {
        get => _exportPeriod;
        set => SetProperty(ref _exportPeriod, value);
    }

    public DateTimeOffset? ExportDate
    {
        get => _exportDate;
        set => SetProperty(ref _exportDate, value);
    }

    public RelayCommand SaveTokenCommand { get; }
    public AsyncCommand ExportCsvCommand { get; }
    public AsyncCommand ClearCourseCommand { get; }

    private void SaveToken()
    {
        _settingsService.ApiToken = ApiToken;
        _logger.Info("Token saved.");
    }

    private async Task ExportCsvAsync(CancellationToken cancellationToken)
    {
        if (SelectedCourse is null || ExportDate is null)
        {
            _logger.Warn("Select course and date for export.");
            return;
        }

        var range = TimeRange.From(DateOnly.FromDateTime(ExportDate.Value.DateTime), ExportPeriod);
        await _exportService.ExportCsvAsync(SelectedCourse.CourseId, range, cancellationToken);
    }

    private async Task ClearCourseAsync(CancellationToken cancellationToken)
    {
        if (SelectedCourse is null)
        {
            _logger.Warn("Select course to clear.");
            return;
        }

        await using var context = _dbContextFactory.CreateDbContext();
        var course = await context.Courses.FirstOrDefaultAsync(x => x.CourseId == SelectedCourse.CourseId, cancellationToken);
        if (course is null)
        {
            return;
        }

        context.DailyMetrics.RemoveRange(context.DailyMetrics.Where(x => x.CourseId == course.Id));
        context.AttemptsRaw.RemoveRange(context.AttemptsRaw.Where(x => x.CourseId == course.Id));
        await context.SaveChangesAsync(cancellationToken);
        _logger.Info("Course data cleared.");
    }

    private async Task LoadCoursesAsync(CancellationToken cancellationToken)
    {
        await using var context = _dbContextFactory.CreateDbContext();
        var courses = await context.Courses.OrderBy(x => x.Title).ToListAsync(cancellationToken);
        Courses.Clear();
        foreach (var course in courses)
        {
            Courses.Add(CourseRow.FromEntity(course));
        }
    }
}
