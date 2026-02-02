namespace StepikAnalyticsDesktop.Domain;

public enum SyncStatus
{
    Ok,
    Syncing,
    Error,
    Never
}

public enum SyncRunStatus
{
    Ok,
    Error,
    Cancelled
}

public enum PeriodKind
{
    Day,
    Week,
    Month,
    Year
}
