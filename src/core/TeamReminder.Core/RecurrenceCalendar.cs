namespace TeamReminder.Core;

public static class RecurrenceCalendar
{
    public static WeekDays DayFlag(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => WeekDays.Monday, DayOfWeek.Tuesday => WeekDays.Tuesday,
        DayOfWeek.Wednesday => WeekDays.Wednesday, DayOfWeek.Thursday => WeekDays.Thursday,
        DayOfWeek.Friday => WeekDays.Friday, DayOfWeek.Saturday => WeekDays.Saturday,
        _ => WeekDays.Sunday
    };

    public static DateTime FirstAfter(DateTime localStart, TimeZoneInfo zone,
        RecurrenceKind recurrence, WeekDays days, DateTime afterUtc)
    {
        if (recurrence != RecurrenceKind.Daily && recurrence != RecurrenceKind.Weekly)
            throw new ArgumentOutOfRangeException(nameof(recurrence));
        if (recurrence == RecurrenceKind.Weekly && (days == WeekDays.None || (days & ~WeekDays.All) != 0))
            throw new ArgumentOutOfRangeException(nameof(days));

        var afterLocal = TimeZoneInfo.ConvertTimeFromUtc(afterUtc, zone);
        var firstDate = localStart.Date > afterLocal.Date ? localStart.Date : afterLocal.Date;
        // Two weeks accommodate a skipped DST date, even for a single selected weekday.
        for (var i = 0; i < 15; i++)
        {
            var date = firstDate.AddDays(i);
            if (recurrence == RecurrenceKind.Weekly && !days.HasFlag(DayFlag(date.DayOfWeek))) continue;
            var candidate = DateTime.SpecifyKind(date + localStart.TimeOfDay, DateTimeKind.Unspecified);
            if (zone.IsInvalidTime(candidate)) continue;
            var utc = zone.IsAmbiguousTime(candidate)
                ? new DateTimeOffset(candidate, zone.GetAmbiguousTimeOffsets(candidate).Max()).UtcDateTime
                : TimeZoneInfo.ConvertTimeToUtc(candidate, zone);
            if (utc > afterUtc) return utc;
        }
        throw new InvalidOperationException("Não foi possível calcular a próxima ocorrência.");
    }

    public static DateTime? NextAfter(Alarm alarm, DateTime nowUtc)
    {
        if (alarm.Recurrence == RecurrenceKind.Once) return null;
        var zone = ResolveZone(alarm.TimeZoneId);
        var start = TimeZoneInfo.ConvertTimeFromUtc(alarm.ScheduledAtUtc, zone);
        return FirstAfter(start, zone, alarm.Recurrence, alarm.Days, nowUtc);
    }

    // Synced and group alarms may carry a zone id this machine does not know (IANA vs Windows, or removed zones).
    // Falling back keeps the alarm firing instead of failing on every tick.
    public static TimeZoneInfo ResolveZone(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return TimeZoneInfo.Local;
        if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zone)) return zone;
        if (TimeZoneInfo.TryConvertIanaIdToWindowsId(id, out var windowsId) && TimeZoneInfo.TryFindSystemTimeZoneById(windowsId, out zone)) return zone;
        if (TimeZoneInfo.TryConvertWindowsIdToIanaId(id, out var ianaId) && TimeZoneInfo.TryFindSystemTimeZoneById(ianaId, out zone)) return zone;
        return TimeZoneInfo.Local;
    }

    public static string Describe(RecurrenceKind recurrence, WeekDays days) => recurrence switch
    {
        RecurrenceKind.Once => "Uma vez",
        RecurrenceKind.Daily => "Todos os dias",
        _ => string.Join(", ", new[]
        {
            (WeekDays.Monday, "Seg"), (WeekDays.Tuesday, "Ter"), (WeekDays.Wednesday, "Qua"),
            (WeekDays.Thursday, "Qui"), (WeekDays.Friday, "Sex"), (WeekDays.Saturday, "Sáb"), (WeekDays.Sunday, "Dom")
        }.Where(day => days.HasFlag(day.Item1)).Select(day => day.Item2))
    };
}
