using LiteDB;
using TeamReminder.Core;

namespace TeamReminder.Local;

public sealed class LiteDbAlarmRepository(string filePath) : IAlarmRepository, IDisposable
{
    private readonly object gate = new();
    private LiteDatabase? database;
    private bool disposed;

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TeamReminder", "Data", "team-reminder.db");

    private ILiteCollection<AlarmDocument> Collection()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (database is null)
        {
            var fullPath = Path.GetFullPath(filePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            var connection = new ConnectionString { Filename = fullPath };
#if DEBUG
            connection.Connection = ConnectionType.Shared;
#else
            connection.Connection = ConnectionType.Direct;
#endif
            var opened = new LiteDatabase(connection, new BsonMapper());
            try
            {
                opened.UtcDate = true;
                opened.GetCollection<AlarmDocument>("alarms").EnsureIndex(a => a.ScheduledAtUtc);
                database = opened;
            }
            catch { opened.Dispose(); throw; }
        }
        return database.GetCollection<AlarmDocument>("alarms");
    }

    public Task InsertAsync(Alarm alarm) => Task.Run(() =>
    {
        lock (gate) { Collection().Insert(ToDocument(alarm)); }
    });

    public Task<IReadOnlyList<Alarm>> ListAsync() => Task.Run<IReadOnlyList<Alarm>>(() =>
    {
        lock (gate)
        {
            return Collection().FindAll().Select(ToAlarm)
                .OrderBy(a => a.DueAtUtc ?? a.ScheduledAtUtc).ThenBy(a => a.CreatedAtUtc).ThenBy(a => a.Id).ToArray();
        }
    });

    public Task<bool> UpdateAsync(Alarm alarm) => Task.Run(() =>
    {
        lock (gate)
        {
            var collection = Collection();
            var existing = collection.FindById(alarm.Id);
            if (existing is null) return false;
            var updated = ToDocument(alarm);
            updated.CreatedAtUtc = existing.CreatedAtUtc;
            return collection.Update(updated);
        }
    });

    public Task<bool> CompleteOccurrenceAsync(Alarm expected, DateTime? nextOccurrenceUtc) => Task.Run(() =>
    {
        lock (gate)
        {
            var collection = Collection();
            var existing = collection.FindById(expected.Id);
            if (existing is null || ToAlarm(existing) != expected) return false;
            existing.LastDeliveredOccurrenceUtc = expected.DueAtUtc;
            existing.NextOccurrenceUtc = nextOccurrenceUtc;
            return collection.Update(existing);
        }
    });

    public Task<bool> DeleteAsync(Guid id) => Task.Run(() =>
    {
        lock (gate) { return Collection().Delete(id); }
    });

    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
    private static DateTime? Utc(DateTime? value) => value.HasValue ? Utc(value.Value) : null;

    private static Alarm ToAlarm(AlarmDocument a) => new(a.Id, a.Title, Utc(a.ScheduledAtUtc), Utc(a.CreatedAtUtc), a.TimeZoneId)
    {
        Recurrence = a.Recurrence, Days = a.Days,
        NextOccurrenceUtc = Utc(a.NextOccurrenceUtc), LastDeliveredOccurrenceUtc = Utc(a.LastDeliveredOccurrenceUtc)
    };

    private static AlarmDocument ToDocument(Alarm a) => new()
    {
        Id = a.Id, Title = a.Title, ScheduledAtUtc = a.ScheduledAtUtc, CreatedAtUtc = a.CreatedAtUtc,
        TimeZoneId = a.TimeZoneId, Recurrence = a.Recurrence, Days = a.Days,
        NextOccurrenceUtc = a.NextOccurrenceUtc, LastDeliveredOccurrenceUtc = a.LastDeliveredOccurrenceUtc
    };

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            database?.Dispose();
        }
    }

    public sealed class AlarmDocument
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = "";
        public DateTime ScheduledAtUtc { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public string TimeZoneId { get; set; } = "";
        public RecurrenceKind Recurrence { get; set; }
        public WeekDays Days { get; set; }
        public DateTime? NextOccurrenceUtc { get; set; }
        public DateTime? LastDeliveredOccurrenceUtc { get; set; }
    }
}
