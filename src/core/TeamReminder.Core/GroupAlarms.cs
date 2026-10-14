namespace TeamReminder.Core;
public sealed record GroupAlarmResponse(Guid Id, Guid GroupId, string GroupName, long Version, bool CanManage, AlarmDefinition Alarm);
public sealed record AlarmDestination(Guid? GroupId, string Label)
{
    public override string ToString() => Label;
}
public interface IGroupAlarmPublisher
{
    Task<Alarm> SaveAsync(Guid groupId, Alarm alarm, long expectedVersion);
    Task DeleteAsync(Alarm alarm);
}
public sealed class GroupAlarmOperationException(string message) : Exception(message);
