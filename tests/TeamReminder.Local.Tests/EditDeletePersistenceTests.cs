using TeamReminder.Core;
using TeamReminder.Local;

namespace TeamReminder.Local.Tests;

public class EditDeletePersistenceTests
{
    [Fact]
    public async Task EditAndDeletionSurviveReopenAndPreserveOtherRecords()
    {
        var directory = Path.Combine(Path.GetTempPath(), "TeamReminderTests", Guid.NewGuid().ToString());
        var path = Path.Combine(directory, "alarms.db");
        var now = new DateTime(2026, 10, 11, 12, 0, 0, DateTimeKind.Utc);
        var original = new Alarm(Guid.NewGuid(), "Consulta", now.AddHours(1), now, "UTC");
        var deleted = original with { Id = Guid.NewGuid(), Title = "Excluir" };
        var untouched = original with { Id = Guid.NewGuid(), Title = "Preservar" };
        var updated = original with { Title = "Consulta alterada", ScheduledAtUtc = now.AddHours(2), TimeZoneId = "E. South America Standard Time" };
        try
        {
            using (var writer = new LiteDbAlarmRepository(path))
            {
                await writer.InsertAsync(original);
                await writer.InsertAsync(deleted);
                await writer.InsertAsync(untouched);
                Assert.True(await writer.UpdateAsync(updated));
                Assert.True(await writer.DeleteAsync(deleted.Id));
            }
            using var reader = new LiteDbAlarmRepository(path);
            var loaded = await reader.ListAsync();
            Assert.Equal(2, loaded.Count);
            Assert.Equal(updated, loaded.Single(a => a.Id == original.Id));
            Assert.Equal(untouched, loaded.Single(a => a.Id == untouched.Id));
            Assert.DoesNotContain(loaded, a => a.Id == deleted.Id);
        }
        finally { Cleanup(directory); }
    }

    [Fact]
    public async Task MissingIdReturnsFalseWithoutInserting()
    {
        var directory = Path.Combine(Path.GetTempPath(), "TeamReminderTests", Guid.NewGuid().ToString());
        var path = Path.Combine(directory, "alarms.db");
        try
        {
            using var repo = new LiteDbAlarmRepository(path);
            var now = new DateTime(2026, 10, 11, 12, 0, 0, DateTimeKind.Utc);
            var absent = new Alarm(Guid.NewGuid(), "Ausente", now.AddHours(1), now, "UTC");
            Assert.False(await repo.UpdateAsync(absent));
            Assert.False(await repo.DeleteAsync(absent.Id));
            Assert.Empty(await repo.ListAsync());
        }
        finally { Cleanup(directory); }
    }

    [Fact]
    public async Task UpdateCannotChangeTheStoredCreationTime()
    {
        var directory = Path.Combine(Path.GetTempPath(), "TeamReminderTests", Guid.NewGuid().ToString());
        var path = Path.Combine(directory, "alarms.db");
        try
        {
            using var repo = new LiteDbAlarmRepository(path);
            var now = new DateTime(2026, 10, 11, 12, 0, 0, DateTimeKind.Utc);
            var original = new Alarm(Guid.NewGuid(), "Consulta", now.AddHours(1), now, "UTC");
            await repo.InsertAsync(original);
            Assert.True(await repo.UpdateAsync(original with { Title = "Atualizado", CreatedAtUtc = now.AddDays(1) }));
            var restored = Assert.Single(await repo.ListAsync());
            Assert.Equal(original.CreatedAtUtc, restored.CreatedAtUtc);
            Assert.Equal("Atualizado", restored.Title);
        }
        finally { Cleanup(directory); }
    }

    [Fact]
    public async Task DisposedRepositoryRejectsEditAndDelete()
    {
        var path = Path.Combine(Path.GetTempPath(), "TeamReminderTests", Guid.NewGuid().ToString(), "alarms.db");
        var now = new DateTime(2026, 10, 11, 12, 0, 0, DateTimeKind.Utc);
        using var repo = new LiteDbAlarmRepository(path);
        repo.Dispose();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => repo.UpdateAsync(new Alarm(Guid.NewGuid(), "Consulta", now, now, "UTC")));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => repo.DeleteAsync(Guid.NewGuid()));
    }

    private static void Cleanup(string directory)
    {
        if (!directory.StartsWith(Path.Combine(Path.GetTempPath(), "TeamReminderTests"), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Invalid test directory");
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }
}
