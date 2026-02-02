using Microsoft.EntityFrameworkCore;
using StepikAnalyticsDesktop.Domain;

namespace StepikAnalyticsDesktop.Data;

public sealed class AppDbContext : DbContext
{
    public DbSet<CourseEntity> Courses => Set<CourseEntity>();
    public DbSet<SyncRunEntity> SyncRuns => Set<SyncRunEntity>();
    public DbSet<DailyMetricEntity> DailyMetrics => Set<DailyMetricEntity>();
    public DbSet<AttemptRawEntity> AttemptsRaw => Set<AttemptRawEntity>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CourseEntity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.CourseId).IsUnique();
            entity.Property(x => x.SyncStatus).HasConversion<string>();
            entity.Property(x => x.Title).HasMaxLength(256);
            entity.Property(x => x.Url).HasMaxLength(512);
        });

        modelBuilder.Entity<SyncRunEntity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasConversion<string>();
            entity.HasOne(x => x.Course)
                .WithMany(x => x.SyncRuns)
                .HasForeignKey(x => x.CourseId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DailyMetricEntity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.CourseId, x.Date }).IsUnique();
            entity.HasOne(x => x.Course)
                .WithMany(x => x.DailyMetrics)
                .HasForeignKey(x => x.CourseId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AttemptRawEntity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.AttemptId).IsUnique();
            entity.HasIndex(x => new { x.CourseId, x.CreatedAt });
            entity.HasIndex(x => new { x.CourseId, x.UserId, x.CreatedAt });
            entity.HasOne(x => x.Course)
                .WithMany(x => x.AttemptsRaw)
                .HasForeignKey(x => x.CourseId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
