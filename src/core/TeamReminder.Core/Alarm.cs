namespace TeamReminder.Core;

public enum RecurrenceKind { Once = 0, Daily = 1, Weekly = 2 }

[Flags]
public enum WeekDays
{
    None = 0, Monday = 1, Tuesday = 2, Wednesday = 4, Thursday = 8,
    Friday = 16, Saturday = 32, Sunday = 64, All = 127
}

public sealed record Alarm(Guid Id, string Title, DateTime ScheduledAtUtc,
    DateTime CreatedAtUtc, string TimeZoneId)
{
    public string? CacheScope { get; init; }
    public AlarmLink? Link { get; init; }
    public AlarmSound Sound { get; init; } = AlarmSound.Default;
    public RecurrenceKind Recurrence { get; init; }
    public WeekDays Days { get; init; }
    public DateTime? NextOccurrenceUtc { get; init; }
    public DateTime? LastDeliveredOccurrenceUtc { get; init; }

    // Older documents have neither field; their initial schedule is still pending.
    public DateTime? DueAtUtc => NextOccurrenceUtc ??
        (LastDeliveredOccurrenceUtc is null ? ScheduledAtUtc : null);
}

public interface IAlarmRepository
{
    Task InsertAsync(Alarm alarm);
    Task<IReadOnlyList<Alarm>> ListAsync();
    Task<bool> UpdateAsync(Alarm alarm);
    Task<bool> UpdateAsync(Alarm original, Alarm updated) => UpdateAsync(updated);
    Task<bool> DeleteAsync(Guid id);
    Task<bool> DeleteAsync(Alarm expected) => DeleteAsync(expected.Id);
    Task<bool> CompleteOccurrenceAsync(Alarm expected, DateTime? nextOccurrenceUtc);
}

public interface IAlarmScopeProvider { string? CurrentScope { get; } }

public sealed class AlarmValidationException(string field, string message) : Exception(message)
{
    public string Field { get; } = field;
}

public sealed class AlarmNotFoundException() : Exception(
    "O alarme não existe mais. Atualize a lista para continuar.");

public sealed class AlarmService(IAlarmRepository repository, TimeProvider clock)
{
    public Task<IReadOnlyList<Alarm>> ListAsync() => repository.ListAsync();

    public async Task<Alarm> CreateAsync(string? title, DateTime? localDate,
        TimeSpan? localTime, TimeZoneInfo zone, RecurrenceKind recurrence = RecurrenceKind.Once,
        WeekDays days = WeekDays.None, AlarmSound? sound = null, string? linkUrl = null, string? linkLabel = null)
    {
        sound ??= AlarmSound.Default;
        sound.Validate();
        var link = AlarmLink.Create(linkUrl, linkLabel);
        var (normalizedTitle, utc) = Validate(title, localDate, localTime, zone, recurrence, days);
        var now = clock.GetUtcNow().UtcDateTime;
        now = new DateTime(now.Ticks - now.Ticks % TimeSpan.TicksPerMillisecond, DateTimeKind.Utc);
        var alarm = new Alarm(Guid.NewGuid(), normalizedTitle, utc, now, zone.Id)
        {
            CacheScope = (repository as IAlarmScopeProvider)?.CurrentScope,
            Sound = sound, Link = link,
            Recurrence = recurrence,
            Days = recurrence == RecurrenceKind.Weekly ? days : WeekDays.None,
            NextOccurrenceUtc = utc
        };
        await repository.InsertAsync(alarm);
        return alarm;
    }

    public async Task<Alarm> UpdateAsync(Alarm original, string? title, DateTime? localDate,
        TimeSpan? localTime, TimeZoneInfo zone, RecurrenceKind recurrence = RecurrenceKind.Once,
        WeekDays days = WeekDays.None, AlarmSound? sound = null, string? linkUrl = null, string? linkLabel = null)
    {
        sound ??= AlarmSound.Default;
        sound.Validate();
        var link = AlarmLink.Create(linkUrl, linkLabel);
        var (normalizedTitle, utc) = Validate(title, localDate, localTime, zone, recurrence, days);
        var updated = original with
        {
            Title = normalizedTitle, ScheduledAtUtc = utc, TimeZoneId = zone.Id,
            Sound = sound, Link = link,
            Recurrence = recurrence, Days = recurrence == RecurrenceKind.Weekly ? days : WeekDays.None,
            NextOccurrenceUtc = utc, LastDeliveredOccurrenceUtc = null
        };
        if (!await repository.UpdateAsync(original, updated)) throw new AlarmNotFoundException();
        return updated;
    }

    public async Task DeleteAsync(Guid id)
    {
        if (!await repository.DeleteAsync(id)) throw new AlarmNotFoundException();
    }

    public async Task DeleteAsync(Alarm expected)
    {
        if (!await repository.DeleteAsync(expected)) throw new AlarmNotFoundException();
    }

    private (string Title, DateTime Utc) Validate(string? title, DateTime? localDate,
        TimeSpan? localTime, TimeZoneInfo zone, RecurrenceKind recurrence, WeekDays days)
    {
        title = title?.Trim();
        if (string.IsNullOrEmpty(title) || title.Length > 120)
            throw new AlarmValidationException("Title", "Informe um título de 1 a 120 caracteres.");
        if (localDate is null || localTime is null)
            throw new AlarmValidationException("Schedule", "Informe a data e o horário.");
        if (localTime < TimeSpan.Zero || localTime >= TimeSpan.FromDays(1))
            throw new AlarmValidationException("Schedule", "Informe um horário válido.");
        if (!Enum.IsDefined(recurrence) || (days & ~WeekDays.All) != 0 ||
            (recurrence == RecurrenceKind.Weekly && days == WeekDays.None))
            throw new AlarmValidationException("Recurrence", "Selecione ao menos um dia da semana para repetir.");

        var now = clock.GetUtcNow().UtcDateTime;
        var local = DateTime.SpecifyKind(localDate.Value.Date + localTime.Value, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(local) || zone.IsAmbiguousTime(local))
            throw new AlarmValidationException("Schedule",
                "Este horário é inexistente ou ambíguo neste fuso. Escolha outro horário.");
        var utc = TimeZoneInfo.ConvertTimeToUtc(local, zone);
        if (recurrence == RecurrenceKind.Once)
        {
            if (utc <= now)
                throw new AlarmValidationException("Schedule", "Escolha uma data e um horário futuros.");
            return (title, utc);
        }
        if (local.Date < TimeZoneInfo.ConvertTimeFromUtc(now, zone).Date)
            throw new AlarmValidationException("Schedule", "Escolha uma data inicial a partir de hoje.");
        var first = RecurrenceCalendar.FirstAfter(local, zone, recurrence, days, now);
        return (title, first);
    }
}
