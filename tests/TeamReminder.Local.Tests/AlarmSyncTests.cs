using TeamReminder.Core;
using TeamReminder.Local;

namespace TeamReminder.Local.Tests;

public sealed class AlarmSyncTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "TeamReminder.Sync.Tests", Guid.NewGuid().ToString("N"));
    private readonly List<IDisposable> repositories = [];
    private LiteDbAlarmRepository Open(string name = "sync.db")
    {
        var repo = new LiteDbAlarmRepository(Path.Combine(root, name), synchronize: true);
        repositories.Add(repo); return repo;
    }
    private static DateTime UtcNow() => new(DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond * TimeSpan.TicksPerMillisecond, DateTimeKind.Utc);
    private static Alarm Alarm() => new(Guid.NewGuid(), "Local", UtcNow().AddHours(1), UtcNow(), "UTC") { NextOccurrenceUtc = UtcNow().AddHours(1) };
    private static PersonalAlarmChange Remote(Alarm alarm, long version, Guid? mutation = null) =>
        new(alarm.Id, version, false, mutation ?? Guid.NewGuid(), AlarmSyncMapping.Definition(alarm));

    [Fact]
    public async Task OfflineQueueSurvivesRestartWithStableMutationId()
    {
        var repo = Open(); var alarm = Alarm(); await repo.InsertAsync(alarm);
        var pending = Assert.Single(await repo.GetPendingAsync());
        repo.Dispose(); var reopened = Open();
        Assert.Equal(pending, Assert.Single(await reopened.GetPendingAsync()));
        Assert.Equal(alarm.Title, Assert.Single(await reopened.ListAsync()).Title);
        await reopened.DeleteAsync(alarm.Id);
        Assert.True(Assert.Single(await reopened.GetPendingAsync()).Request.Deleted);
    }

    [Fact]
    public async Task AcknowledgementDoesNotLoseEditsMadeDuringRequest()
    {
        var repo = Open(); var alarm = Alarm(); await repo.InsertAsync(alarm);
        var sent = Assert.Single(await repo.GetPendingAsync());
        await repo.UpdateAsync(alarm with { Title = "Editada durante envio" });
        await repo.AcknowledgeAsync(sent, Remote(alarm, 1, sent.Request.MutationId));
        var next = Assert.Single(await repo.GetPendingAsync());
        Assert.Equal(1, next.Request.ExpectedVersion);
        Assert.NotEqual(sent.Request.MutationId, next.Request.MutationId);
        Assert.Equal("Editada durante envio", next.Request.Alarm!.Title);
    }

    [Fact]
    public async Task LostResponseIsAcknowledgedOnNextPullWithoutRepeatingDelivery()
    {
        var repo = Open(); var alarm = Alarm(); await repo.InsertAsync(alarm);
        var sent = Assert.Single(await repo.GetPendingAsync());
        var current = Assert.Single(await repo.ListAsync());
        Assert.True(await repo.CompleteOccurrenceAsync(current, null));
        await repo.ApplyChangesAsync(new(1, [Remote(alarm, 1, sent.Request.MutationId)]));
        Assert.Empty(await repo.GetPendingAsync());
        Assert.Null(Assert.Single(await repo.ListAsync()).DueAtUtc);
    }

    [Fact]
    public async Task RemoteMetadataUpdatePreservesLocalDeliveryReceipt()
    {
        var repo = Open(); var alarm = Alarm();
        await repo.ApplyChangesAsync(new(1, [Remote(alarm, 1)]));
        await repo.CompleteOccurrenceAsync(Assert.Single(await repo.ListAsync()), null);
        await repo.ApplyChangesAsync(new(2, [Remote(alarm with { Title = "Novo título" }, 2)]));
        var updated = Assert.Single(await repo.ListAsync());
        Assert.Equal("Novo título", updated.Title); Assert.Null(updated.DueAtUtc);
        Assert.Empty(await repo.GetPendingAsync());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ConflictsPreserveLocalDataUntilExplicitResolution(bool keepLocal)
    {
        var repo = Open(); var alarm = Alarm();
        await repo.ApplyChangesAsync(new(1, [Remote(alarm, 1)]));
        await repo.UpdateAsync(alarm with { Title = "Minha edição" });
        await repo.ApplyChangesAsync(new(2, [Remote(alarm with { Title = "Outro computador" }, 2)]));
        Assert.Equal("Minha edição", Assert.Single(await repo.ListAsync()).Title);
        Assert.Empty(await repo.GetPendingAsync()); Assert.Single(await repo.GetConflictsAsync());
        await repo.ResolveConflictAsync(alarm.Id, keepLocal);
        Assert.Empty(await repo.GetConflictsAsync());
        Assert.Equal(keepLocal ? "Minha edição" : "Outro computador", Assert.Single(await repo.ListAsync()).Title);
        if (keepLocal) Assert.Equal(2, Assert.Single(await repo.GetPendingAsync()).Request.ExpectedVersion);
        else Assert.Empty(await repo.GetPendingAsync());
    }

    [Fact]
    public async Task RemoteDeletionRemovesAlarmWithoutCreatingOutgoingDeletion()
    {
        var repo = Open(); var alarm = Alarm();
        await repo.ApplyChangesAsync(new(1, [Remote(alarm, 1)]));
        await repo.ApplyChangesAsync(new(2, [new(alarm.Id, 2, true, Guid.NewGuid(), null)]));
        Assert.Empty(await repo.ListAsync()); Assert.Empty(await repo.GetPendingAsync()); Assert.Equal(2, await repo.GetCursorAsync());
    }

    [Fact]
    public async Task FailedBatchRollsBackAlarmChangesAndCursor()
    {
        var repo = Open(); var alarm = Alarm();
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.ApplyChangesAsync(new(2,
            [Remote(alarm, 1), new(Guid.NewGuid(), 2, false, Guid.NewGuid(), null)])));
        Assert.Empty(await repo.ListAsync()); Assert.Equal(0, await repo.GetCursorAsync());
    }

    [Fact]
    public async Task CachedHistoricalAlarmDoesNotFireAsNewNotification()
    {
        var repo = Open(); var alarm = Alarm() with { ScheduledAtUtc = UtcNow().AddDays(-1) };
        await repo.ApplyChangesAsync(new(1, [Remote(alarm, 1)]));
        Assert.Null(Assert.Single(await repo.ListAsync()).DueAtUtc);
    }

    [Fact]
    public async Task AccountsAndServersAreIsolatedAndImportRequiresExplicitCall()
    {
        using var repo = new AccountAlarmRepository(Path.Combine(root, "local.db"));
        var original = Alarm(); await repo.InsertAsync(original);
        var accountA = Guid.NewGuid(); var accountB = Guid.NewGuid();
        repo.SelectAccount("https://server-a/", accountA);
        Assert.Empty(await repo.ListAsync());
        Assert.Equal(1, await repo.ImportLocalAsync()); Assert.Equal(0, await repo.ImportLocalAsync());
        var imported = Assert.Single(await repo.ListAsync());
        repo.SelectAccount("https://server-a/", accountB);
        Assert.Empty(await repo.ListAsync()); await repo.ImportLocalAsync();
        Assert.False(await repo.CompleteOccurrenceAsync(imported, null));
        Assert.False(await repo.UpdateAsync(imported)); Assert.False(await repo.DeleteAsync(imported));
        repo.SelectAccount("https://server-b/", accountA); Assert.Empty(await repo.ListAsync());
        repo.SelectAccount(); Assert.Equal(original.Id, Assert.Single(await repo.ListAsync()).Id);
        repo.SelectAccount("https://server-a/", accountA); Assert.Single(await repo.Current.GetPendingAsync());
    }

    [Fact]
    public async Task NewlyCreatedAccountAlarmCanBeEditedImmediately()
    {
        using var repo = new AccountAlarmRepository(Path.Combine(root, "local.db"));
        repo.SelectAccount("https://server/", Guid.NewGuid());
        var service = new AlarmService(repo, TimeProvider.System);
        var tomorrow = DateTime.UtcNow.AddDays(1);
        var alarm = await service.CreateAsync("Criado", tomorrow.Date, TimeSpan.FromHours(12), TimeZoneInfo.Utc);
        await service.UpdateAsync(alarm, "Editado", tomorrow.Date, TimeSpan.FromHours(13), TimeZoneInfo.Utc);
        Assert.Equal("Editado", Assert.Single(await repo.ListAsync()).Title);
    }

    public void Dispose()
    {
        foreach (var repository in repositories) repository.Dispose();
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}
