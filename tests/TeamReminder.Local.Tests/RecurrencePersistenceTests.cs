using LiteDB;
using TeamReminder.Core;
using TeamReminder.Local;

namespace TeamReminder.Local.Tests;

public class RecurrencePersistenceTests
{
    private static readonly DateTime Now = new(2026, 10, 12, 13, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task WeeklyRecurrenceAndReceiptSurviveReopening()
    {
        var directory = Path.Combine(Path.GetTempPath(), "TeamReminderTests", Guid.NewGuid().ToString());
        var path = Path.Combine(directory, "alarms.db");
        var original = new Alarm(Guid.NewGuid(), "Treino", Now, Now.AddDays(-1), "UTC")
        { Recurrence = RecurrenceKind.Weekly, Days = WeekDays.Monday | WeekDays.Wednesday | WeekDays.Friday, NextOccurrenceUtc = Now };
        try
        {
            using (var repo = new LiteDbAlarmRepository(path))
            {
                await repo.InsertAsync(original);
                Assert.True(await repo.CompleteOccurrenceAsync(Assert.Single(await repo.ListAsync()), Now.AddDays(2)));
            }
            using var reopened = new LiteDbAlarmRepository(path);
            var alarm = Assert.Single(await reopened.ListAsync());
            Assert.Equal(original.Id, alarm.Id);
            Assert.Equal(original.Days, alarm.Days);
            Assert.Equal(RecurrenceKind.Weekly, alarm.Recurrence);
            Assert.Equal(Now, alarm.LastDeliveredOccurrenceUtc);
            Assert.Equal(Now.AddDays(2), alarm.DueAtUtc);
            Assert.Equal(DateTimeKind.Utc, alarm.NextOccurrenceUtc!.Value.Kind);
        }
        finally { Cleanup(directory); }
    }

    [Fact]
    public async Task LegacyDocumentWithoutNewFieldsRemainsAnUndeliveredOnceAlarm()
    {
        var directory = Path.Combine(Path.GetTempPath(), "TeamReminderTests", Guid.NewGuid().ToString());
        var path = Path.Combine(directory, "alarms.db");
        Directory.CreateDirectory(directory);
        var id = Guid.NewGuid();
        try
        {
            using (var database = new LiteDatabase(path))
            {
                database.UtcDate = true;
                database.GetCollection("alarms").Insert(new BsonDocument
                {
                    ["_id"] = id, ["Title"] = "Antigo", ["ScheduledAtUtc"] = Now,
                    ["CreatedAtUtc"] = Now.AddDays(-1), ["TimeZoneId"] = "UTC"
                });
            }
            using var repo = new LiteDbAlarmRepository(path);
            var alarm = Assert.Single(await repo.ListAsync());
            Assert.Equal(id, alarm.Id);
            Assert.Equal("Antigo", alarm.Title);
            Assert.Equal(RecurrenceKind.Once, alarm.Recurrence);
            Assert.Equal(Now, alarm.DueAtUtc);
            Assert.Null(alarm.LastDeliveredOccurrenceUtc);
            Assert.True(await repo.CompleteOccurrenceAsync(alarm, null));
            Assert.Null(Assert.Single(await repo.ListAsync()).DueAtUtc);
        }
        finally { Cleanup(directory); }
    }

    [Fact]
    public async Task OnceReceiptPreventsDeliveryAfterRestart()
    {
        var directory = Path.Combine(Path.GetTempPath(), "TeamReminderTests", Guid.NewGuid().ToString());
        var path = Path.Combine(directory, "alarms.db");
        var notifier = new CounterNotifier();
        try
        {
            using (var first = new LiteDbAlarmRepository(path))
            {
                await first.InsertAsync(new Alarm(Guid.NewGuid(), "Consulta", Now, Now.AddDays(-1), "UTC"));
                await new AlarmScheduler(first, notifier, new Clock(Now)).TickAsync();
            }
            using var second = new LiteDbAlarmRepository(path);
            await new AlarmScheduler(second, notifier, new Clock(Now.AddDays(1))).TickAsync();
            Assert.Equal(1, notifier.Count);
            Assert.Null(Assert.Single(await second.ListAsync()).DueAtUtc);
        }
        finally { Cleanup(directory); }
    }

    [Fact]
    public async Task OldCompletionCannotOverwriteNewScheduleOrRecreateDeletedRecord()
    {
        var directory = Path.Combine(Path.GetTempPath(), "TeamReminderTests", Guid.NewGuid().ToString());
        var path = Path.Combine(directory, "alarms.db");
        try
        {
            using var repo = new LiteDbAlarmRepository(path);
            var original = new Alarm(Guid.NewGuid(), "Consulta", Now, Now.AddDays(-1), "UTC");
            await repo.InsertAsync(original);
            var oldSnapshot = Assert.Single(await repo.ListAsync());
            var changed = oldSnapshot with { ScheduledAtUtc = Now.AddHours(1), NextOccurrenceUtc = Now.AddHours(1) };
            Assert.True(await repo.UpdateAsync(changed));
            Assert.False(await repo.CompleteOccurrenceAsync(oldSnapshot, null));
            Assert.Equal(changed, Assert.Single(await repo.ListAsync()));
            Assert.True(await repo.DeleteAsync(original.Id));
            Assert.False(await repo.CompleteOccurrenceAsync(changed, null));
            Assert.Empty(await repo.ListAsync());
        }
        finally { Cleanup(directory); }
    }

    private sealed class Clock(DateTime now) : TimeProvider
    { public override DateTimeOffset GetUtcNow() => new(now); }
    private sealed class CounterNotifier : IAlarmNotifier
    {
        public int Count { get; private set; }
        public Task NotifyAsync(AlarmDelivery delivery) { Count++; return Task.CompletedTask; }
    }
    private static void Cleanup(string directory)
    {
        if (!directory.StartsWith(Path.Combine(Path.GetTempPath(), "TeamReminderTests"), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Invalid test directory");
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }
}
