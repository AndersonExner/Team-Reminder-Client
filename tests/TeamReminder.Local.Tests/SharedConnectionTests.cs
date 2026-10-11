#if DEBUG
using LiteDB;
using TeamReminder.Core;
using TeamReminder.Local;

namespace TeamReminder.Local.Tests;

public class SharedConnectionTests
{
    [Fact]
    public async Task DebugAllowsExternalSharedConnectionWhileRepositoryIsOpen()
    {
        var directory = Path.Combine(Path.GetTempPath(), "TeamReminderTests", Guid.NewGuid().ToString());
        var path = Path.Combine(directory, "alarms.db");
        try
        {
            using var repo = new LiteDbAlarmRepository(path);
            var now = new DateTime(2026, 10, 11, 12, 0, 0, DateTimeKind.Utc);
            await repo.InsertAsync(new Alarm(Guid.NewGuid(), "Inspeção", now.AddHours(1), now, "UTC"));
            using var external = new LiteDatabase(new ConnectionString
            {
                Filename = path,
                Connection = ConnectionType.Shared
            });
            Assert.Equal(1, external.GetCollection("alarms").Count());
            await repo.InsertAsync(new Alarm(Guid.NewGuid(), "Segundo", now.AddHours(2), now, "UTC"));
            Assert.Equal(2, external.GetCollection("alarms").Count());
            Assert.Equal(2, (await repo.ListAsync()).Count);
        }
        finally
        {
            if (!directory.StartsWith(Path.Combine(Path.GetTempPath(), "TeamReminderTests"), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Invalid test path");
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
#endif
