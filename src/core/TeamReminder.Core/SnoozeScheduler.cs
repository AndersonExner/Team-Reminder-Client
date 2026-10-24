namespace TeamReminder.Core;

public sealed record SnoozeEntry(Guid AlarmId, DateTime OccurrenceUtc, DateTime UntilUtc);

public interface ISnoozeStore
{
    Task<IReadOnlyList<SnoozeEntry>> LoadAsync();
    Task SaveAsync(IReadOnlyList<SnoozeEntry> entries);
}

// Snoozing is local to this computer, like delivery receipts: the alarm itself (and its sync state) is untouched.
public sealed class SnoozeScheduler(IAlarmRepository repository, ISnoozeStore store, IAlarmNotifier notifier, TimeProvider clock)
{
    public static readonly int[] Options = [5, 10, 15];
    private readonly SemaphoreSlim gate = new(1, 1);
    private List<SnoozeEntry>? entries;

    public async Task<DateTime> SnoozeAsync(Guid alarmId, DateTime occurrenceUtc, TimeSpan delay)
    {
        if (delay <= TimeSpan.Zero || delay > TimeSpan.FromHours(24)) throw new ArgumentOutOfRangeException(nameof(delay));
        await gate.WaitAsync();
        try
        {
            var list = await EntriesAsync();
            var until = clock.GetUtcNow().UtcDateTime + delay;
            list.RemoveAll(entry => entry.AlarmId == alarmId);
            list.Add(new SnoozeEntry(alarmId, occurrenceUtc, until));
            await store.SaveAsync(list.ToArray());
            return until;
        }
        finally { gate.Release(); }
    }

    public async Task<IReadOnlyList<SnoozeEntry>> PendingAsync()
    {
        await gate.WaitAsync();
        try { return (await EntriesAsync()).OrderBy(entry => entry.UntilUtc).ToArray(); }
        finally { gate.Release(); }
    }

    public async Task<IReadOnlyList<string>> TickAsync()
    {
        if (!await gate.WaitAsync(0)) return [];
        try
        {
            var list = await EntriesAsync();
            var now = clock.GetUtcNow().UtcDateTime;
            var due = list.Where(entry => entry.UntilUtc <= now).ToArray();
            if (due.Length == 0) return [];
            var errors = new List<string>();
            var alarms = await repository.ListAsync();
            foreach (var entry in due)
            {
                // A deleted alarm, or one moved to another account, is not announced again.
                var alarm = alarms.FirstOrDefault(a => a.Id == entry.AlarmId);
                try
                {
                    if (alarm is not null)
                        await notifier.NotifyAsync(new AlarmDelivery(alarm, entry.OccurrenceUtc, false, IsSnoozed: true));
                    list.Remove(entry);
                }
                catch (Exception)
                {
                    errors.Add($"Não foi possível repetir o alarme adiado {alarm?.Title}. Uma nova tentativa será feita.");
                }
            }
            await store.SaveAsync(list.ToArray());
            return errors;
        }
        finally { gate.Release(); }
    }

    private async Task<List<SnoozeEntry>> EntriesAsync() => entries ??= [.. await store.LoadAsync()];
}
