using TeamReminder.Core;

namespace TeamReminder.Windows;

public sealed class AuthenticatedAlarmApi(AuthClient api, Func<Task<string>> accessToken, Func<Task> refresh, Func<Task> waitForEditor) : IAlarmSyncApi
{
    private async Task<T> Execute<T>(Func<string, Task<T>> operation)
    {
        try { return await operation(await accessToken()); }
        catch (AccountApiException ex) when (ex.Unauthorized)
        { await refresh(); return await operation(await accessToken()); }
    }
    public async Task<AlarmChangeSet> PullAsync(long cursor)
    { var result = await Execute(token => api.PullAlarmsAsync(cursor, token)); await waitForEditor(); return result; }
    public Task<PersonalAlarmChange> PushAsync(PendingAlarmWrite write) => Execute(token => api.PushAlarmAsync(write, token));
}
