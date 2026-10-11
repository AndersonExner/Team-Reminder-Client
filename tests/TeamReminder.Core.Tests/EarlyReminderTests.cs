using TeamReminder.Core;

namespace TeamReminder.Core.Tests;

public class EarlyReminderTests
{
    private static readonly DateTime Due = new(2026, 10, 12, 15, 30, 0, DateTimeKind.Utc);
    private static Alarm Sample() => new(Guid.NewGuid(), "Consulta", Due, Due.AddDays(-1), "UTC");

    [Theory]
    [InlineData(null, -10, 0)]
    [InlineData(7, -7, 0)]
    [InlineData(10, -11, 0)]
    [InlineData(10, -10, 1)]
    [InlineData(10, -2, 1)]
    [InlineData(10, 0, 0)]
    [InlineData(10, 5, 0)]
    public async Task RespectsAdvanceWindowAndNeverDeliversAtOrAfterMainTime(int? minutes, int offset, int expected)
    {
        var repo = new MemoryRepository();
        var alarm = Sample(); repo.Alarms.Add(alarm);
        var store = new EarlyStore(repo);
        var count = 0;
        var scheduler = new EarlyReminderScheduler(repo, store, _ => { count++; return Task.CompletedTask; },
            () => minutes, new FixedClock(Due.AddMinutes(offset)));
        Assert.Empty(await scheduler.TickAsync());
        Assert.Equal(expected, count);
        Assert.Equal(alarm, Assert.Single(repo.Alarms)); // Never completes/reprograms the main alarm.
    }

    [Fact]
    public async Task RestartAndChangingAdvanceDoNotRepeatSameOccurrence()
    {
        var repo = new MemoryRepository(); repo.Alarms.Add(Sample());
        var store = new EarlyStore(repo);
        var clock = new FixedClock(Due.AddMinutes(-5));
        var count = 0;
        Task Notify(Alarm _) { count++; return Task.CompletedTask; }
        var scheduler = new EarlyReminderScheduler(repo, store, Notify, () => 10, clock);
        await scheduler.TickAsync();
        await scheduler.TickAsync();
        Assert.False(scheduler.ShouldCheck(repo.Alarms));
        await new EarlyReminderScheduler(repo, store, Notify, () => 30, clock).TickAsync();
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task SendFailureRetriesAndReceiptFailureDoesNotResendInSameProcess()
    {
        var repo = new MemoryRepository(); repo.Alarms.Add(Sample());
        var store = new EarlyStore(repo) { FailWrite = true };
        var failSend = true; var count = 0;
        var scheduler = new EarlyReminderScheduler(repo, store, _ =>
        {
            if (failSend) throw new IOException();
            count++; return Task.CompletedTask;
        }, () => 10, new FixedClock(Due.AddMinutes(-5)));
        Assert.Single(await scheduler.TickAsync());
        Assert.Empty(store.Delivered);
        failSend = false;
        Assert.Single(await scheduler.TickAsync());
        Assert.Single(await scheduler.TickAsync());
        Assert.Equal(1, count);
        store.FailWrite = false;
        Assert.Empty(await scheduler.TickAsync());
        Assert.Equal(Due, store.Delivered[repo.Alarms[0].Id]);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task RecurringAlarmReceivesNextOccurrenceWithoutReplayingOldReceipt()
    {
        var alarm = Sample() with { Recurrence = RecurrenceKind.Daily };
        var repo = new MemoryRepository(); repo.Alarms.Add(alarm);
        var store = new EarlyStore(repo);
        var clock = new FixedClock(Due.AddMinutes(-10));
        var count = 0;
        var scheduler = new EarlyReminderScheduler(repo, store, _ => { count++; return Task.CompletedTask; }, () => 10, clock);
        await scheduler.TickAsync();
        await repo.CompleteOccurrenceAsync(alarm, Due.AddDays(1));
        clock.Utc = Due.AddDays(1).AddMinutes(-10);
        await scheduler.TickAsync();
        Assert.Equal(2, count);
        Assert.Equal(Due.AddDays(1), repo.Alarms[0].DueAtUtc);
    }

    [Fact]
    public async Task ConcurrentTicksDoNotDuplicateNotification()
    {
        var repo = new MemoryRepository(); repo.Alarms.Add(Sample());
        var store = new EarlyStore(repo);
        var gate = new TaskCompletionSource();
        var count = 0;
        var scheduler = new EarlyReminderScheduler(repo, store, async _ => { count++; await gate.Task; },
            () => 10, new FixedClock(Due.AddMinutes(-5)));
        var first = scheduler.TickAsync();
        Assert.Empty(await scheduler.TickAsync());
        gate.SetResult(); await first;
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task MainAlarmStillDeliversAfterEarlyNotificationFailure()
    {
        var repo = new MemoryRepository(); repo.Alarms.Add(Sample());
        var clock = new FixedClock(Due.AddMinutes(-5));
        var early = new EarlyReminderScheduler(repo, new EarlyStore(repo), _ => throw new IOException(), () => 10, clock);
        Assert.Single(await early.TickAsync());
        clock.Utc = Due;
        var notifier = new RecordingNotifier();
        await new AlarmScheduler(repo, notifier, clock).TickAsync();
        Assert.Single(notifier.Deliveries);
        Assert.Null(repo.Alarms[0].DueAtUtc);
    }

    private sealed class EarlyStore(MemoryRepository repo) : IEarlyReminderReceiptStore
    {
        public Dictionary<Guid, DateTime> Delivered { get; } = [];
        public bool FailWrite { get; set; }
        public Task<DateTime?> GetEarlyReminderOccurrenceAsync(Guid id) =>
            Task.FromResult<DateTime?>(Delivered.TryGetValue(id, out var date) ? date : null);
        public Task<bool> RecordEarlyReminderAsync(Alarm expected)
        {
            if (FailWrite) throw new IOException();
            if (!repo.Alarms.Contains(expected) || expected.DueAtUtc is not { } due) return Task.FromResult(false);
            Delivered[expected.Id] = due;
            return Task.FromResult(true);
        }
    }
}
