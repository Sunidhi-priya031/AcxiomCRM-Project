namespace AcxiomCRM.Infrastructure;

/// <summary>Date filter used by the dashboard: Today, This Week, This Month, Custom Range or All Time.</summary>
public class DateRange
{
    public const string AllTime = "all";
    public const string Today = "today";
    public const string ThisWeek = "week";
    public const string ThisMonth = "month";
    public const string Custom = "custom";

    public string Period { get; init; } = AllTime;

    /// <summary>Inclusive start (date at 00:00).</summary>
    public DateTime? From { get; init; }

    /// <summary>Exclusive end (the day after the last included day, at 00:00).</summary>
    public DateTime? ToExclusive { get; init; }

    public string Label { get; init; } = "All time";

    public static DateRange Resolve(string? period, DateTime? from, DateTime? to)
    {
        var today = DateTime.Today;
        switch (period)
        {
            case Today:
                return new DateRange { Period = Today, From = today, ToExclusive = today.AddDays(1), Label = "Today" };
            case ThisWeek:
                // Weeks start on Monday.
                var offset = ((int)today.DayOfWeek + 6) % 7;
                var weekStart = today.AddDays(-offset);
                return new DateRange { Period = ThisWeek, From = weekStart, ToExclusive = weekStart.AddDays(7), Label = "This week" };
            case ThisMonth:
                var monthStart = new DateTime(today.Year, today.Month, 1);
                return new DateRange { Period = ThisMonth, From = monthStart, ToExclusive = monthStart.AddMonths(1), Label = "This month" };
            case Custom when from.HasValue || to.HasValue:
                var start = from?.Date;
                var end = to?.Date;
                if (start.HasValue && end.HasValue && end < start)
                {
                    (start, end) = (end, start);
                }

                var label = $"{(start.HasValue ? start.Value.ToString("dd MMM yyyy") : "Beginning")} – {(end.HasValue ? end.Value.ToString("dd MMM yyyy") : "Today")}";
                return new DateRange { Period = Custom, From = start, ToExclusive = end?.AddDays(1), Label = label };
            default:
                return new DateRange { Period = AllTime, Label = "All time" };
        }
    }

    public bool Contains(DateTime value) =>
        (!From.HasValue || value >= From.Value) && (!ToExclusive.HasValue || value < ToExclusive.Value);
}
