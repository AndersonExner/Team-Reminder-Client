using LiteDB;
using TeamReminder.Core;
using TeamReminder.Local;

namespace TeamReminder.Local.Tests;

public class AlarmLinkPersistenceTests
{
    [Fact]
    public async Task LinkSurvivesReopenAndDeliveryAndCanBeUpdatedAndRemoved()
    {
        var root = Path.Combine(Path.GetTempPath(), "TeamReminderTests", Guid.NewGuid().ToString());
        var path = Path.Combine(root, "alarms.db"); var now = new DateTime(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc);
        var alarm = new Alarm(Guid.NewGuid(), "Ponto", now.AddHours(1), now, "UTC")
            { Link = AlarmLink.Create("https://example.com/ponto?a=1&b=2#fim", "Bater ponto") };
        try
        {
            using (var repo = new LiteDbAlarmRepository(path)) await repo.InsertAsync(alarm);
            using (var repo = new LiteDbAlarmRepository(path))
            {
                var loaded = Assert.Single(await repo.ListAsync()); Assert.Equal(alarm, loaded);
                Assert.True(await repo.CompleteOccurrenceAsync(loaded, now.AddDays(1)));
            }
            using (var repo = new LiteDbAlarmRepository(path))
            {
                var loaded = Assert.Single(await repo.ListAsync()); Assert.Equal(alarm.Link, loaded.Link);
                Assert.True(await repo.UpdateAsync(loaded with { Link = AlarmLink.Create("http://intranet/ponto", null) }));
            }
            using (var repo = new LiteDbAlarmRepository(path))
            {
                var loaded = Assert.Single(await repo.ListAsync()); Assert.Equal("http://intranet/ponto", loaded.Link!.Url);
                Assert.Equal("Abrir link", loaded.Link.Label);
                Assert.True(await repo.UpdateAsync(loaded with { Link = null }));
            }
            using var reopened = new LiteDbAlarmRepository(path);
            Assert.Null(Assert.Single(await reopened.ListAsync()).Link);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task LegacyDocumentWithoutLinkFieldsLoadsWithoutAction()
    {
        var root = Path.Combine(Path.GetTempPath(), "TeamReminderTests", Guid.NewGuid().ToString());
        var path = Path.Combine(root, "alarms.db"); Directory.CreateDirectory(root);
        try
        {
            using (var db = new LiteDatabase(path))
                db.GetCollection("alarms").Insert(new BsonDocument {
                    ["_id"] = Guid.NewGuid(), ["Title"] = "Antigo", ["TimeZoneId"] = "UTC",
                    ["ScheduledAtUtc"] = DateTime.UtcNow, ["CreatedAtUtc"] = DateTime.UtcNow });
            using var repo = new LiteDbAlarmRepository(path);
            Assert.Null(Assert.Single(await repo.ListAsync()).Link);
        }
        finally { Directory.Delete(root, true); }
    }
}
