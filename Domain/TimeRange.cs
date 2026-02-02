using System;
using System.Collections.Generic;
using System.Globalization;

namespace StepikAnalyticsDesktop.Domain;

public sealed record TimeRange(DateOnly Start, DateOnly End, PeriodKind Period)
{
    public static TimeRange From(DateOnly anchor, PeriodKind period)
    {
        return period switch
        {
            PeriodKind.Day => new TimeRange(anchor, anchor, period),
            PeriodKind.Week => CreateWeek(anchor),
            PeriodKind.Month => CreateMonth(anchor),
            PeriodKind.Year => CreateYear(anchor),
            _ => throw new ArgumentOutOfRangeException(nameof(period), period, "Unsupported period")
        };
    }

    public static TimeRange CreateWeek(DateOnly anchor)
    {
        var dayOfWeek = (int)anchor.DayOfWeek;
        var isoDay = dayOfWeek == 0 ? 7 : dayOfWeek;
        var start = anchor.AddDays(1 - isoDay);
        var end = start.AddDays(6);
        return new TimeRange(start, end, PeriodKind.Week);
    }

    public static TimeRange CreateMonth(DateOnly anchor)
    {
        var start = new DateOnly(anchor.Year, anchor.Month, 1);
        var end = start.AddMonths(1).AddDays(-1);
        return new TimeRange(start, end, PeriodKind.Month);
    }

    public static TimeRange CreateYear(DateOnly anchor)
    {
        var start = new DateOnly(anchor.Year, 1, 1);
        var end = new DateOnly(anchor.Year, 12, 31);
        return new TimeRange(start, end, PeriodKind.Year);
    }

    public IEnumerable<DateOnly> EachDay()
    {
        for (var date = Start; date <= End; date = date.AddDays(1))
        {
            yield return date;
        }
    }

    public TimeRange Previous()
    {
        return Period switch
        {
            PeriodKind.Day => new TimeRange(Start.AddDays(-1), End.AddDays(-1), Period),
            PeriodKind.Week => new TimeRange(Start.AddDays(-7), End.AddDays(-7), Period),
            PeriodKind.Month => CreateMonth(Start.AddMonths(-1)),
            PeriodKind.Year => CreateYear(new DateOnly(Start.Year - 1, 1, 1)),
            _ => throw new ArgumentOutOfRangeException(nameof(Period), Period, "Unsupported period")
        };
    }

    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Start:yyyy-MM-dd}..{End:yyyy-MM-dd}");
}
