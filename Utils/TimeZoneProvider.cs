using System;
using System.Collections.Generic;
using System.Linq;

namespace StepikAnalyticsDesktop.Utils;

public static class TimeZoneProvider
{
    public static IReadOnlyList<TimeZoneInfo> GetTimeZones()
    {
        return TimeZoneInfo.GetSystemTimeZones();
    }

    public static TimeZoneInfo GetById(string id)
    {
        return TimeZoneInfo.FindSystemTimeZoneById(id);
    }

    public static DateOnly ToLocalDate(DateTimeOffset timestamp, TimeZoneInfo timeZone)
    {
        var local = TimeZoneInfo.ConvertTime(timestamp, timeZone);
        return DateOnly.FromDateTime(local.DateTime);
    }

    public static (DateTimeOffset StartUtc, DateTimeOffset EndUtc) GetUtcRange(DateOnly date, TimeZoneInfo timeZone)
    {
        var startLocal = date.ToDateTime(TimeOnly.MinValue);
        var endLocal = date.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var startUtc = TimeZoneInfo.ConvertTimeToUtc(startLocal, timeZone);
        var endUtc = TimeZoneInfo.ConvertTimeToUtc(endLocal, timeZone);
        return (new DateTimeOffset(startUtc), new DateTimeOffset(endUtc));
    }
}
