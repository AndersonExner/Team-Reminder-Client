using LiteDB;
using TeamReminder.Core;
using TeamReminder.Local;

namespace TeamReminder.Local.Tests;

public class AlarmSoundPersistenceTests
{
    [Theory]
    [InlineData(AlarmSoundKind.Default)]
    [InlineData(AlarmSoundKind.Notification)]
    [InlineData(AlarmSoundKind.Exclamation)]
    [InlineData(AlarmSoundKind.Critical)]
    [InlineData(AlarmSoundKind.Silent)]
    [InlineData(AlarmSoundKind.Custom)]
    public async Task ReopeningPreservesSoundAndOccurrenceCompletionDoesNotResetIt(AlarmSoundKind kind)
    {
        var path = Path.Combine(Path.GetTempPath(), "TeamReminderTests", Guid.NewGuid().ToString(), "alarms.db");
        var now = new DateTime(2026, 10, 11, 12, 0, 0, DateTimeKind.Utc);
        var sound = kind == AlarmSoundKind.Custom ? new AlarmSound(kind, Guid.NewGuid(), "música.mp3") : new AlarmSound(kind);
        var alarm = new Alarm(Guid.NewGuid(), "Consulta", now.AddHours(1), now, "UTC") { Sound = sound };
        try
        {
            using (var repo = new LiteDbAlarmRepository(path)) await repo.InsertAsync(alarm);
            using (var repo = new LiteDbAlarmRepository(path))
            {
                var loaded = Assert.Single(await repo.ListAsync()); Assert.Equal(alarm, loaded);
                Assert.True(await repo.CompleteOccurrenceAsync(loaded, now.AddDays(1)));
            }
            using var reader = new LiteDbAlarmRepository(path);
            Assert.Equal(sound, Assert.Single(await reader.ListAsync()).Sound);
        }
        finally { Directory.Delete(Path.GetDirectoryName(path)!, true); }
    }

    [Fact]
    public async Task OldDocumentsWithoutSoundFieldsRemainDefaultAfterEditingAndReopening()
    {
        var path = Path.Combine(Path.GetTempPath(), "TeamReminderTests", Guid.NewGuid().ToString(), "alarms.db");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var now = new DateTime(2026, 10, 11, 12, 0, 0, DateTimeKind.Utc);
        try
        {
            using (var db = new LiteDatabase(path))
                db.GetCollection("alarms").Insert(new BsonDocument {
                    ["_id"] = Guid.NewGuid(), ["Title"] = "Antigo", ["ScheduledAtUtc"] = now.AddHours(1),
                    ["CreatedAtUtc"] = now, ["TimeZoneId"] = "UTC" });
            using (var repo = new LiteDbAlarmRepository(path))
            {
                var old = Assert.Single(await repo.ListAsync()); Assert.Equal(AlarmSound.Default, old.Sound);
                Assert.True(await repo.UpdateAsync(old with { Sound = new AlarmSound(AlarmSoundKind.Silent) }));
            }
            using var reader = new LiteDbAlarmRepository(path);
            Assert.Equal(AlarmSoundKind.Silent, Assert.Single(await reader.ListAsync()).Sound.Kind);
        }
        finally { Directory.Delete(Path.GetDirectoryName(path)!, true); }
    }
}
