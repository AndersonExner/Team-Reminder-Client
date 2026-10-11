using TeamReminder.Core;
using TeamReminder.Local;

namespace TeamReminder.Local.Tests;

public class PersistenceTests
{
    [Fact]
    public async Task ReopeningFilePreservesIdentityFieldsAndUtcTimes()
    {
        var path = Path.Combine(Path.GetTempPath(), "TeamReminderTests", Guid.NewGuid().ToString(), "alarms.db");
        var alarm = new Alarm(Guid.NewGuid(), "Consulta", new DateTime(2026, 10, 12, 14, 30, 0, DateTimeKind.Utc),
            new DateTime(2026, 10, 11, 12, 0, 0, DateTimeKind.Utc), "E. South America Standard Time");
        try
        {
            using (var writer = new LiteDbAlarmRepository(path)) await writer.InsertAsync(alarm);
            using var reader = new LiteDbAlarmRepository(path);
            var restored = Assert.Single(await reader.ListAsync());
            Assert.Equal(alarm, restored);
            Assert.Equal(DateTimeKind.Utc, restored.ScheduledAtUtc.Kind);
            Assert.Equal(DateTimeKind.Utc, restored.CreatedAtUtc.Kind);
        }
        finally { Cleanup(path); }
    }

    [Fact]
    public async Task ConcurrentWritesPersistAllAlarmsAndReadChronologically()
    {
        var path = Path.Combine(Path.GetTempPath(), "TeamReminderTests", Guid.NewGuid().ToString(), "alarms.db");
        try
        {
            using var repo = new LiteDbAlarmRepository(path);
            var now = new DateTime(2026, 10, 11, 12, 0, 0, DateTimeKind.Utc);
            var alarms = Enumerable.Range(1, 20).Reverse().Select(i =>
                new Alarm(Guid.NewGuid(), "Alarme " + i, now.AddHours(i), now, "UTC")).ToArray();
            await Task.WhenAll(alarms.Select(repo.InsertAsync));
            var loaded = await repo.ListAsync();
            Assert.Equal(20, loaded.Count);
            Assert.Equal(alarms.OrderBy(a => a.ScheduledAtUtc).Select(a => a.Id), loaded.Select(a => a.Id));
        }
        finally { Cleanup(path); }
    }

    [Fact]
    public async Task InvalidDatabaseIsPreservedAfterReadFailure()
    {
        var path = Path.Combine(Path.GetTempPath(), "TeamReminderTests", Guid.NewGuid().ToString(), "alarms.db");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var content = new byte[8192];
        Random.Shared.NextBytes(content);
        await File.WriteAllBytesAsync(path, content);
        try
        {
            using (var repo = new LiteDbAlarmRepository(path))
                await Assert.ThrowsAnyAsync<Exception>(() => repo.ListAsync());
            Assert.Equal(content, await File.ReadAllBytesAsync(path));
        }
        finally { Cleanup(path); }
    }

    [Fact]
    public async Task DisposedRepositoryRejectsOperations()
    {
        var path = Path.Combine(Path.GetTempPath(), "TeamReminderTests", Guid.NewGuid().ToString(), "alarms.db");
        var repo = new LiteDbAlarmRepository(path);
        repo.Dispose();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => repo.ListAsync());
    }

    private static void Cleanup(string path)
    {
        var directory = Path.GetDirectoryName(path)!;
        if (!directory.StartsWith(Path.Combine(Path.GetTempPath(), "TeamReminderTests"), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Invalid test path");
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }
}
