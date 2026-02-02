using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using StepikAnalyticsDesktop.Data;
using StepikAnalyticsDesktop.Domain;
using StepikAnalyticsDesktop.Services;
using StepikAnalyticsDesktop.Utils;

namespace StepikAnalyticsDesktop.ViewModels;

public sealed class CoursesViewModel : ViewModelBase
{
    private readonly SqliteDbContextFactory _dbContextFactory;
    private readonly SyncService _syncService;
    private readonly UiLogger _logger;
    private string _courseInput = string.Empty;

    public CoursesViewModel(SqliteDbContextFactory dbContextFactory, SyncService syncService, UiLogger logger)
    {
        _dbContextFactory = dbContextFactory;
        _syncService = syncService;
        _logger = logger;

        AddCourseCommand = new AsyncCommand(AddCourseAsync);
        SyncCourseCommand = new AsyncCommand(SyncCourseAsync);
        DeleteCourseCommand = new AsyncCommand(DeleteCourseAsync);
        OpenCourseCommand = new RelayCommand(OpenCourse);

        _ = LoadAsync(CancellationToken.None);
    }

    public ObservableCollection<CourseRow> Courses { get; } = new();

    public string CourseInput
    {
        get => _courseInput;
        set => SetProperty(ref _courseInput, value);
    }

    public AsyncCommand AddCourseCommand { get; }
    public AsyncCommand SyncCourseCommand { get; }
    public AsyncCommand DeleteCourseCommand { get; }
    public RelayCommand OpenCourseCommand { get; }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        await using var context = _dbContextFactory.CreateDbContext();
        var courses = await context.Courses.AsNoTracking().OrderBy(x => x.Title).ToListAsync(cancellationToken);
        Courses.Clear();
        foreach (var course in courses)
        {
            Courses.Add(CourseRow.FromEntity(course));
        }
    }

    private async Task AddCourseAsync(CancellationToken cancellationToken)
    {
        var parsedId = ParseCourseId(CourseInput);
        if (!parsedId.HasValue)
        {
            _logger.Warn("Course ID not recognized.");
            return;
        }

        await using var context = _dbContextFactory.CreateDbContext();
        var existing = await context.Courses.FirstOrDefaultAsync(x => x.CourseId == parsedId.Value, cancellationToken);
        if (existing is not null)
        {
            _logger.Warn("Course already exists.");
            return;
        }

        var entity = new CourseEntity
        {
            CourseId = parsedId.Value,
            Title = $"Course {parsedId.Value}",
            Url = CourseInput,
            AddedAt = DateTimeOffset.UtcNow,
            SyncStatus = SyncStatus.Never
        };
        context.Courses.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        Courses.Add(CourseRow.FromEntity(entity));
        CourseInput = string.Empty;
        _logger.Info($"Course {parsedId.Value} added.");
    }

    private async Task SyncCourseAsync(CancellationToken cancellationToken)
    {
        if (SelectedCourse is null)
        {
            _logger.Warn("Select a course to sync.");
            return;
        }

        await _syncService.SyncCourseAsync(SelectedCourse.CourseId, cancellationToken);
        await LoadAsync(cancellationToken);
    }

    private async Task DeleteCourseAsync(CancellationToken cancellationToken)
    {
        if (SelectedCourse is null)
        {
            _logger.Warn("Select a course to delete.");
            return;
        }

        await using var context = _dbContextFactory.CreateDbContext();
        var entity = await context.Courses.FirstOrDefaultAsync(x => x.CourseId == SelectedCourse.CourseId, cancellationToken);
        if (entity is null)
        {
            return;
        }

        context.Courses.Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
        Courses.Remove(SelectedCourse);
        _logger.Info($"Course {SelectedCourse.CourseId} deleted.");
    }

    private void OpenCourse(object? parameter)
    {
        if (parameter is CourseRow row && Uri.TryCreate(row.Url, UriKind.Absolute, out var uri))
        {
            _logger.Info($"Open course URL: {uri}");
        }
    }

    private CourseRow? _selectedCourse;

    public CourseRow? SelectedCourse
    {
        get => _selectedCourse;
        set => SetProperty(ref _selectedCourse, value);
    }

    private static int? ParseCourseId(string input)
    {
        if (int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
        {
            return id;
        }

        var digits = new string(input.Where(char.IsDigit).ToArray());
        if (int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out id))
        {
            return id;
        }

        return null;
    }
}

public sealed record CourseRow(
    string Title,
    int CourseId,
    string Url,
    string SyncStatus,
    string? LastSyncAt)
{
    public static CourseRow FromEntity(CourseEntity entity)
    {
        return new CourseRow(
            entity.Title,
            entity.CourseId,
            entity.Url,
            entity.SyncStatus.ToString(),
            entity.LastSyncAt?.ToString("g", CultureInfo.CurrentCulture));
    }
}
