using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using StepikAnalyticsDesktop.Data;
using StepikAnalyticsDesktop.Domain;
using StepikAnalyticsDesktop.Utils;

namespace StepikAnalyticsDesktop.Services;

public static class CourseSeeder
{
    private static readonly int[] DefaultCourseIds = { 202590, 210038, 199780 };

    public static async Task SeedAsync(SqliteDbContextFactory dbContextFactory, UiLogger logger, CancellationToken cancellationToken)
    {
        await using var context = dbContextFactory.CreateDbContext();
        var existing = await context.Courses.Select(x => x.CourseId).ToListAsync(cancellationToken);
        var toAdd = DefaultCourseIds.Except(existing).ToList();

        if (toAdd.Count == 0)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var courseId in toAdd)
        {
            context.Courses.Add(new CourseEntity
            {
                CourseId = courseId,
                Title = $"Course {courseId}",
                Url = $"https://stepik.org/course/{courseId}",
                AddedAt = now,
                SyncStatus = SyncStatus.Never
            });
        }

        await context.SaveChangesAsync(cancellationToken);
        logger.Info($"Seeded courses: {string.Join(", ", toAdd)}");
    }
}
