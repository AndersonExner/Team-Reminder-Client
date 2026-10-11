using TeamReminder.Core;
using TeamReminder.Local;

namespace TeamReminder.Local.Tests;

public class EarlyReceiptTests
{
    private static Alarm Sample() => new(Guid.NewGuid(), "Consulta", new DateTime(2026, 10, 12, 15, 30, 0, DateTimeKind.Utc),
        new DateTime(2026, 10, 11, 12, 0, 0, DateTimeKind.Utc), "UTC");

    [Fact]
    public async Task ReceiptSurvivesReopenAndLeavesAlarmUnchanged()
    {
        var path = Path.Combine(Path.GetTempPath(), "TeamReminderTests", Guid.NewGuid().ToString(), "alarms.db");
        var alarm = Sample();
        try
        {
            using (var repo = new LiteDbAlarmRepository(path))
            {
                await repo.InsertAsync(alarm);
                Assert.Null(await repo.GetEarlyReminderOccurrenceAsync(alarm.Id));
                Assert.True(await repo.RecordEarlyReminderAsync(alarm));
                Assert.Equal(alarm, Assert.Single(await repo.ListAsync()));
            }
            using var reopened = new LiteDbAlarmRepository(path);
            Assert.Equal(alarm.DueAtUtc, await reopened.GetEarlyReminderOccurrenceAsync(alarm.Id));
            Assert.Equal(DateTimeKind.Utc, (await reopened.GetEarlyReminderOccurrenceAsync(alarm.Id))!.Value.Kind);
        }
        finally { Directory.Delete(Path.GetDirectoryName(path)!, true); }
    }

    [Fact]
    public async Task StaleOrDeletedSnapshotsCannotWriteAndDeleteCleansReceipt()
    {
        var path = Path.Combine(Path.GetTempPath(), "TeamReminderTests", Guid.NewGuid().ToString(), "alarms.db");
        try
        {
            using var repo = new LiteDbAlarmRepository(path);
            var alarm = Sample(); await repo.InsertAsync(alarm);
            var edited = alarm with { Title = "Alterado", NextOccurrenceUtc = alarm.ScheduledAtUtc.AddHours(1) };
            await repo.UpdateAsync(edited);
            Assert.False(await repo.RecordEarlyReminderAsync(alarm));
            Assert.Null(await repo.GetEarlyReminderOccurrenceAsync(alarm.Id));
            Assert.True(await repo.RecordEarlyReminderAsync(edited));
            await repo.DeleteAsync(alarm.Id);
            Assert.Null(await repo.GetEarlyReminderOccurrenceAsync(alarm.Id));
            Assert.False(await repo.RecordEarlyReminderAsync(edited));
        }
        finally { Directory.Delete(Path.GetDirectoryName(path)!, true); }
    }
}
