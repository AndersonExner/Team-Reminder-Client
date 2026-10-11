namespace TeamReminder.Core;

public interface IEarlyReminderReceiptStore
{
    Task<DateTime?> GetEarlyReminderOccurrenceAsync(Guid alarmId);
    Task<bool> RecordEarlyReminderAsync(Alarm expected);
}

public sealed class EarlyReminderScheduler(IAlarmRepository repository, IEarlyReminderReceiptStore receipts,
    Func<Alarm, Task> notify, Func<int?> advanceMinutes, TimeProvider clock)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly HashSet<(Guid Id, DateTime Due)> sent = [];
    private readonly HashSet<(Guid Id, DateTime Due)> completed = [];

    private bool IsEligible(Alarm alarm)
    {
        var minutes = advanceMinutes();
        if (minutes is not (5 or 10 or 15 or 30 or 60) || alarm.DueAtUtc is not { } due) return false;
        var now = clock.GetUtcNow().UtcDateTime;
        return due > now && due - now <= TimeSpan.FromMinutes(minutes.Value);
    }

    public bool ShouldCheck(IEnumerable<Alarm> alarms) => alarms.Any(alarm =>
        IsEligible(alarm) && !completed.Contains((alarm.Id, alarm.DueAtUtc!.Value)));

    public async Task<IReadOnlyList<string>> TickAsync()
    {
        if (!await gate.WaitAsync(0)) return [];
        try
        {
            var alarms = await repository.ListAsync();
            var current = alarms.Where(a => a.DueAtUtc is not null)
                .Select(a => (a.Id, a.DueAtUtc!.Value)).ToHashSet();
            sent.RemoveWhere(key => !current.Contains(key));
            completed.RemoveWhere(key => !current.Contains(key));
            var errors = new List<string>();
            foreach (var alarm in alarms.Where(IsEligible))
            {
                var key = (alarm.Id, alarm.DueAtUtc!.Value);
                if (completed.Contains(key)) continue;
                try
                {
                    if (await receipts.GetEarlyReminderOccurrenceAsync(alarm.Id) == key.Value)
                    { completed.Add(key); continue; }
                    // Recheck the time after storage access; never send a late pre-reminder.
                    if (!IsEligible(alarm)) continue;
                    if (!sent.Contains(key))
                    {
                        await notify(alarm);
                        sent.Add(key);
                    }
                    if (await receipts.RecordEarlyReminderAsync(alarm)) completed.Add(key);
                }
                catch (Exception)
                {
                    errors.Add($"Não foi possível concluir o lembrete antecipado de {alarm.Title}. Uma nova tentativa será feita.");
                }
            }
            return errors;
        }
        finally { gate.Release(); }
    }
}
