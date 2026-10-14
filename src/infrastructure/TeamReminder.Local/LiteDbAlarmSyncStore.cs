using JsonSerializer = System.Text.Json.JsonSerializer;
using LiteDB;
using TeamReminder.Core;

namespace TeamReminder.Local;

public sealed partial class LiteDbAlarmRepository : IAlarmSyncStore
{
    public sealed class SyncDocument
    {
        public Guid Id { get; set; }
        public long Version { get; set; }
        public Guid MutationId { get; set; }
        public bool Pending { get; set; }
        public bool Deleted { get; set; }
        public string? DefinitionJson { get; set; }
        public string? ConflictJson { get; set; }
    }
    public sealed class SyncCursor { public int Id { get; set; } = 1; public long Cursor { get; set; } }

    private ILiteCollection<SyncDocument> SyncCollection()
    {
        Collection();
        return database!.GetCollection<SyncDocument>("alarmSync");
    }

    private T InTransaction<T>(Func<T> operation)
    {
        Collection();
        database!.BeginTrans();
        try { var result = operation(); database.Commit(); return result; }
        catch { database.Rollback(); throw; }
    }

    private void QueueChange(Guid id, Alarm? alarm, bool deleted)
    {
        if (!synchronize) return;
        var collection = SyncCollection();
        var state = collection.FindById(id) ?? new SyncDocument { Id = id };
        state.MutationId = Guid.NewGuid(); state.Pending = true; state.Deleted = deleted;
        state.DefinitionJson = alarm is null ? null : JsonSerializer.Serialize(AlarmSyncMapping.Definition(alarm));
        collection.Upsert(state);
    }

    public Task<bool> UpdateAsync(Alarm original, Alarm updated) => Task.Run(() =>
    {
        lock (gate)
        {
            var collection = Collection();
            var existing = collection.FindById(original.Id);
            if (existing is null || existing.GroupId is not null || AlarmSyncMapping.Definition(ToAlarm(existing)) != AlarmSyncMapping.Definition(original)) return false;
            return InTransaction(() =>
            {
                var document = ToDocument(updated);
                document.CreatedAtUtc = existing.CreatedAtUtc;
                var success = collection.Update(document);
                if (success) QueueChange(updated.Id, ToAlarm(document), false);
                return success;
            });
        }
    });

    public Task<long> GetCursorAsync() => Task.Run(() =>
    {
        lock (gate) { Collection(); return database!.GetCollection<SyncCursor>("syncCursor").FindById(1)?.Cursor ?? 0; }
    });

    public Task<IReadOnlyList<PendingAlarmWrite>> GetPendingAsync() => Task.Run<IReadOnlyList<PendingAlarmWrite>>(() =>
    {
        lock (gate) return SyncCollection().FindAll().Where(x => x.Pending && x.ConflictJson is null)
            .Select(x => new PendingAlarmWrite(x.Id, new AlarmWriteRequest(x.Version, x.MutationId, x.Deleted,
                x.DefinitionJson is null ? null : JsonSerializer.Deserialize<AlarmDefinition>(x.DefinitionJson)))).ToArray();
    });

    public Task ApplyChangesAsync(AlarmChangeSet changes) => Task.Run(() =>
    {
        lock (gate) InTransaction(() =>
        {
            var collection = SyncCollection();
            var currentCursor = database!.GetCollection<SyncCursor>("syncCursor").FindById(1)?.Cursor ?? 0;
            if (changes.Cursor < currentCursor) throw new InvalidOperationException("Cursor regressivo.");
            foreach (var remote in changes.Changes)
            {
                var state = collection.FindById(remote.Id) ?? new SyncDocument { Id = remote.Id };
                if (remote.Version <= state.Version) continue;
                if (state.Pending && state.MutationId != remote.MutationId)
                    state.ConflictJson = JsonSerializer.Serialize(remote);
                else
                {
                    // An acknowledged local mutation must preserve its per-device delivery receipt.
                    if (!state.Pending) ApplyRemote(remote);
                    state.Version = remote.Version; state.Pending = false; state.Deleted = remote.Deleted;
                    state.MutationId = remote.MutationId; state.ConflictJson = null;
                }
                collection.Upsert(state);
            }
            database.GetCollection<SyncCursor>("syncCursor").Upsert(new SyncCursor { Cursor = changes.Cursor });
            return true;
        });
    });

    public Task AcknowledgeAsync(PendingAlarmWrite sent, PersonalAlarmChange response) => Task.Run(() =>
    {
        lock (gate) InTransaction(() =>
        {
            var collection = SyncCollection();
            var state = collection.FindById(sent.Id) ?? throw new InvalidOperationException("Mutação local ausente.");
            state.Version = response.Version;
            if (state.MutationId == sent.Request.MutationId) { state.Pending = false; state.ConflictJson = null; }
            collection.Upsert(state);
            return true;
        });
    });

    public Task RecordConflictAsync(PersonalAlarmChange remote) => Task.Run(() =>
    {
        lock (gate)
        {
            var collection = SyncCollection();
            var state = collection.FindById(remote.Id) ?? throw new InvalidOperationException("Mutação local ausente.");
            state.ConflictJson = JsonSerializer.Serialize(remote);
            collection.Upsert(state);
        }
    });

    public Task<IReadOnlyList<AlarmConflict>> GetConflictsAsync() => Task.Run<IReadOnlyList<AlarmConflict>>(() =>
    {
        lock (gate) return SyncCollection().FindAll().Where(x => x.ConflictJson is not null).Select(x =>
        {
            var remote = JsonSerializer.Deserialize<PersonalAlarmChange>(x.ConflictJson!)!;
            var title = x.DefinitionJson is null ? remote.Alarm?.Title : JsonSerializer.Deserialize<AlarmDefinition>(x.DefinitionJson)?.Title;
            return new AlarmConflict(x.Id, title ?? "Alarme excluído", remote);
        }).ToArray();
    });

    public Task ResolveConflictAsync(Guid id, bool keepLocal) => Task.Run(() =>
    {
        lock (gate) InTransaction(() =>
        {
            var collection = SyncCollection();
            var state = collection.FindById(id);
            if (state?.ConflictJson is null) return false;
            var remote = JsonSerializer.Deserialize<PersonalAlarmChange>(state.ConflictJson)!;
            state.Version = remote.Version; state.ConflictJson = null;
            if (keepLocal) { state.MutationId = Guid.NewGuid(); state.Pending = true; }
            else { ApplyRemote(remote); state.Pending = false; state.Deleted = remote.Deleted; state.MutationId = remote.MutationId; }
            collection.Upsert(state);
            return true;
        });
    });

    private void ApplyRemote(PersonalAlarmChange change)
    {
        var collection = Collection();
        if (change.Deleted)
        {
            collection.Delete(change.Id);
            database!.GetCollection<EarlyReminderReceipt>("earlyReminderReceipts").Delete(change.Id);
            return;
        }
        var existing = collection.FindById(change.Id);
        var alarm = AlarmSyncMapping.FromRemote(change, existing is null ? null : ToAlarm(existing), (clock ?? TimeProvider.System).GetUtcNow().UtcDateTime);
        collection.Upsert(ToDocument(alarm));
    }
}
