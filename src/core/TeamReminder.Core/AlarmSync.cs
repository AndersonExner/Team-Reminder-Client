namespace TeamReminder.Core;

public sealed record AlarmDefinition(string Title, DateTimeOffset ScheduledAtUtc, DateTimeOffset CreatedAtUtc,
    string TimeZoneId, int Recurrence, int Days, string? LinkUrl, string? LinkLabel, int SoundKind);
public sealed record AlarmWriteRequest(long ExpectedVersion, Guid MutationId, bool Deleted, AlarmDefinition? Alarm);
public sealed record PersonalAlarmChange(Guid Id, long Version, bool Deleted, Guid MutationId, AlarmDefinition? Alarm);
public sealed record AlarmChangeSet(long Cursor, IReadOnlyList<PersonalAlarmChange> Changes);
public sealed record PendingAlarmWrite(Guid Id, AlarmWriteRequest Request);
public sealed record AlarmConflict(Guid Id, string Title, PersonalAlarmChange Remote);

public interface IAlarmSyncStore
{
    Task<long> GetCursorAsync();
    Task<IReadOnlyList<PendingAlarmWrite>> GetPendingAsync();
    Task ApplyChangesAsync(AlarmChangeSet changes);
    Task AcknowledgeAsync(PendingAlarmWrite sent, PersonalAlarmChange response);
    Task RecordConflictAsync(PersonalAlarmChange remote);
    Task<IReadOnlyList<AlarmConflict>> GetConflictsAsync();
    Task ResolveConflictAsync(Guid id, bool keepLocal);
}

public interface IAlarmSyncApi
{
    Task<AlarmChangeSet> PullAsync(long cursor);
    Task<PersonalAlarmChange> PushAsync(PendingAlarmWrite write);
}

public sealed class AlarmSyncConflictException(PersonalAlarmChange current) : Exception("Alteração simultânea.")
{
    public PersonalAlarmChange Current { get; } = current;
}

public sealed class AlarmSyncEngine(IAlarmSyncStore store, IAlarmSyncApi api)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    public async Task SyncAsync()
    {
        if (!await gate.WaitAsync(0)) return;
        try
        {
            await store.ApplyChangesAsync(await api.PullAsync(await store.GetCursorAsync()));
            foreach (var write in await store.GetPendingAsync())
            {
                try { await store.AcknowledgeAsync(write, await api.PushAsync(write)); }
                catch (AlarmSyncConflictException ex) { await store.RecordConflictAsync(ex.Current); }
            }
            await store.ApplyChangesAsync(await api.PullAsync(await store.GetCursorAsync()));
        }
        finally { gate.Release(); }
    }
}

public static class AlarmSyncMapping
{
    public static AlarmDefinition Definition(Alarm alarm) => new(alarm.Title,
        new DateTimeOffset(DateTime.SpecifyKind(alarm.ScheduledAtUtc, DateTimeKind.Utc)),
        new DateTimeOffset(DateTime.SpecifyKind(alarm.CreatedAtUtc, DateTimeKind.Utc)), alarm.TimeZoneId,
        (int)alarm.Recurrence, (int)alarm.Days, alarm.Link?.Url, alarm.Link?.Label,
        alarm.Sound.Kind == AlarmSoundKind.Custom ? 0 : (int)alarm.Sound.Kind);

    public static Alarm FromRemote(PersonalAlarmChange change, Alarm? existing, DateTime nowUtc)
    {
        var data = change.Alarm ?? throw new InvalidOperationException("Alarme remoto sem conteúdo.");
        var alarm = new Alarm(change.Id, data.Title, data.ScheduledAtUtc.UtcDateTime, data.CreatedAtUtc.UtcDateTime, data.TimeZoneId)
        {
            Recurrence = (RecurrenceKind)data.Recurrence, Days = (WeekDays)data.Days,
            Link = AlarmLink.Create(data.LinkUrl, data.LinkLabel),
            Sound = existing?.Sound.Kind == AlarmSoundKind.Custom ? existing.Sound : new AlarmSound((AlarmSoundKind)data.SoundKind)
        };
        if (existing is not null && existing.ScheduledAtUtc == alarm.ScheduledAtUtc &&
            existing.Recurrence == alarm.Recurrence && existing.Days == alarm.Days && existing.TimeZoneId == alarm.TimeZoneId)
            return alarm with { NextOccurrenceUtc = existing.NextOccurrenceUtc, LastDeliveredOccurrenceUtc = existing.LastDeliveredOccurrenceUtc };
        if (alarm.Recurrence != RecurrenceKind.Once && alarm.ScheduledAtUtc < nowUtc)
            return alarm with { NextOccurrenceUtc = RecurrenceCalendar.NextAfter(alarm, nowUtc) };
        if (alarm.ScheduledAtUtc < nowUtc.AddMinutes(-5))
            return alarm with { LastDeliveredOccurrenceUtc = alarm.ScheduledAtUtc };
        return alarm with { NextOccurrenceUtc = alarm.ScheduledAtUtc };
    }
}
