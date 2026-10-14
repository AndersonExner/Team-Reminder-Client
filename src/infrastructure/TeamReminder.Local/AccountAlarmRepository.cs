using System.Security.Cryptography;
using System.Text;
using TeamReminder.Core;

namespace TeamReminder.Local;

// Separate database files keep account caches, queues and cursors isolated across account/server changes.
public sealed class AccountAlarmRepository(string localPath) : IAlarmRepository, IAlarmScopeProvider, IEarlyReminderReceiptStore, IDisposable
{
    private readonly object gate = new();
    private readonly Dictionary<string, LiteDbAlarmRepository> caches = [];
    private readonly LiteDbAlarmRepository local = new(localPath);
    private string? scope;
    private string AccountDirectory => Path.Combine(Path.GetDirectoryName(Path.GetFullPath(localPath))!, "Accounts");
    public string? Scope { get { lock (gate) return scope; } }
    public string? CurrentScope => Scope;
    public LiteDbAlarmRepository Current { get { lock (gate) return scope is null ? local : caches[scope]; } }

    public void SelectAccount(string? server = null, Guid? accountId = null)
    {
        lock (gate)
        {
            scope = server is null || accountId is null ? null : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(server + "|" + accountId)));
            if (scope is not null && !caches.ContainsKey(scope))
                caches[scope] = new LiteDbAlarmRepository(Path.Combine(AccountDirectory, scope + ".db"), synchronize: true);
        }
    }

    public async Task<IReadOnlyList<Alarm>> ListAsync()
    {
        LiteDbAlarmRepository selected; string? key;
        lock (gate) { selected = Current; key = scope; }
        return (await selected.ListAsync()).Select(x => x with { CacheScope = key }).ToArray();
    }
    public Task InsertAsync(Alarm alarm)
    { lock (gate) return alarm.CacheScope == scope ? Current.InsertAsync(alarm with { CacheScope = null }) : Task.FromException(new AlarmNotFoundException()); }
    public Task<bool> UpdateAsync(Alarm alarm)
    { lock (gate) return alarm.CacheScope == scope ? Current.UpdateAsync(alarm with { CacheScope = null }) : Task.FromResult(false); }
    public Task<bool> UpdateAsync(Alarm original, Alarm updated)
    { lock (gate) return original.CacheScope == scope ? Current.UpdateAsync(original with { CacheScope = null }, updated with { CacheScope = null }) : Task.FromResult(false); }
    public Task<bool> DeleteAsync(Guid id) { lock (gate) return Current.DeleteAsync(id); }
    public Task<bool> DeleteAsync(Alarm expected)
    { lock (gate) return expected.CacheScope == scope ? Current.DeleteAsync(expected.Id) : Task.FromResult(false); }
    public Task<bool> CompleteOccurrenceAsync(Alarm expected, DateTime? next)
    { lock (gate) return expected.CacheScope == scope ? Current.CompleteOccurrenceAsync(expected with { CacheScope = null }, next) : Task.FromResult(false); }
    public Task<DateTime?> GetEarlyReminderOccurrenceAsync(Guid id) { lock (gate) return Current.GetEarlyReminderOccurrenceAsync(id); }
    public Task<bool> RecordEarlyReminderAsync(Alarm expected)
    { lock (gate) return expected.CacheScope == scope ? Current.RecordEarlyReminderAsync(expected with { CacheScope = null }) : Task.FromResult(false); }

    public async Task<int> ImportLocalAsync()
    {
        LiteDbAlarmRepository target;
        lock (gate) { if (scope is null) throw new InvalidOperationException("Entre na conta antes de importar."); target = Current; }
        var existing = (await target.ListAsync()).Select(x => x.Id).ToHashSet();
        var count = 0;
        foreach (var alarm in await local.ListAsync())
            if (existing.Add(alarm.Id)) { await target.InsertAsync(alarm); count++; }
        return count;
    }

    // Sound cleanup must retain files referenced by inactive account caches too.
    public async Task<IReadOnlyList<Alarm>> ListAllCachedAsync()
    {
        var alarms = new List<Alarm>(await local.ListAsync());
        LiteDbAlarmRepository[] repositories;
        lock (gate)
        {
            if (Directory.Exists(AccountDirectory))
                foreach (var file in Directory.EnumerateFiles(AccountDirectory, "*.db"))
                {
                    var key = Path.GetFileNameWithoutExtension(file);
                    if (!caches.ContainsKey(key)) caches[key] = new LiteDbAlarmRepository(file, synchronize: true);
                }
            repositories = caches.Values.ToArray();
        }
        foreach (var repository in repositories) alarms.AddRange(await repository.ListAsync());
        return alarms;
    }

    public void Dispose()
    { lock (gate) { local.Dispose(); foreach (var repository in caches.Values) repository.Dispose(); } }
}
