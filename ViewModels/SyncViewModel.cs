using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using StepikAnalyticsDesktop.Data;
using StepikAnalyticsDesktop.Services;
using StepikAnalyticsDesktop.Utils;

namespace StepikAnalyticsDesktop.ViewModels;

public sealed class SyncViewModel : ViewModelBase
{
    private readonly SyncService _syncService;
    private readonly UiLogger _logger;
    private readonly SqliteDbContextFactory _dbContextFactory;
    private double _progress;
    private string _currentCourseStatus = "";

    public SyncViewModel(SyncService syncService, SqliteDbContextFactory dbContextFactory, UiLogger logger)
    {
        _syncService = syncService;
        _logger = logger;
        _dbContextFactory = dbContextFactory;

        SyncAllCommand = new AsyncCommand(SyncAllAsync);
        StopCommand = new RelayCommand(_ => _syncService.Cancel());
        LogLines = logger.Lines;
        _ = LoadRunsAsync(CancellationToken.None);
    }

    public AsyncCommand SyncAllCommand { get; }
    public RelayCommand StopCommand { get; }

    public ObservableCollection<string> LogLines { get; }
    public ObservableCollection<SyncRunRow> RecentRuns { get; } = new();

    public double Progress
    {
        get => _progress;
        set => SetProperty(ref _progress, value);
    }

    public string CurrentCourseStatus
    {
        get => _currentCourseStatus;
        set => SetProperty(ref _currentCourseStatus, value);
    }

    private async Task SyncAllAsync(CancellationToken cancellationToken)
    {
        CurrentCourseStatus = "Синхронизация всех курсов...";
        Progress = 0;
        await _syncService.SyncAllAsync(cancellationToken);
        Progress = 100;
        CurrentCourseStatus = "Синхронизация завершена";
        await LoadRunsAsync(cancellationToken);
    }

    private async Task LoadRunsAsync(CancellationToken cancellationToken)
    {
        await using var context = _dbContextFactory.CreateDbContext();
        var runs = await context.SyncRuns
            .Include(x => x.Course)
            .OrderByDescending(x => x.StartedAt)
            .Take(20)
            .ToListAsync(cancellationToken);

        RecentRuns.Clear();
        foreach (var run in runs)
        {
            var courseId = run.Course?.CourseId ?? run.CourseId;
            RecentRuns.Add(new SyncRunRow(courseId, run.StartedAt, run.FinishedAt, run.Status.ToString(), run.ErrorText));
        }
    }
}

public sealed record SyncRunRow(int CourseId, DateTimeOffset StartedAt, DateTimeOffset? FinishedAt, string Status, string? ErrorText);
