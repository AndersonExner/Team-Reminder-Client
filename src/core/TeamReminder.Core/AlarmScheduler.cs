namespace TeamReminder.Core;

public sealed record AlarmDelivery(Alarm Alarm, DateTime OccurrenceUtc, bool IsLate);

public interface IAlarmNotifier
{
    Task NotifyAsync(AlarmDelivery delivery);
}

public sealed record AlarmTickResult(IReadOnlyList<Alarm> Alarms, IReadOnlyList<string> Errors);

public sealed class AlarmScheduler(IAlarmRepository repository, IAlarmNotifier notifier, TimeProvider clock)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly HashSet<Alarm> notified = [];

    public async Task<AlarmTickResult?> TickAsync()
    {
        if (!await gate.WaitAsync(0)) return null;
        try
        {
            var alarms = await repository.ListAsync();
            notified.RemoveWhere(alarm => !alarms.Contains(alarm));
            var errors = new List<string>();
            foreach (var alarm in alarms.Where(a => a.DueAtUtc is { } due && due <= clock.GetUtcNow().UtcDateTime))
            {
                try
                {
                    var due = alarm.DueAtUtc!.Value;
                    var next = RecurrenceCalendar.NextAfter(alarm, clock.GetUtcNow().UtcDateTime);
                    if (!notified.Contains(alarm))
                    {
                        await notifier.NotifyAsync(new AlarmDelivery(alarm, due,
                            clock.GetUtcNow().UtcDateTime - due > TimeSpan.FromMinutes(1)));
                        notified.Add(alarm);
                    }
                    // A changed/deleted alarm must never be overwritten by an old delivery.
                    await repository.CompleteOccurrenceAsync(alarm, next);
                }
                catch (Exception)
                {
                    errors.Add($"Não foi possível concluir o alerta de {alarm.Title}. Uma nova tentativa será feita.");
                }
            }
            return new AlarmTickResult(await repository.ListAsync(), errors);
        }
        finally { gate.Release(); }
    }
}
