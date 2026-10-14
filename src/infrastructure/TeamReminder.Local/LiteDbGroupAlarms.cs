using TeamReminder.Core;
namespace TeamReminder.Local;
public sealed partial class LiteDbAlarmRepository
{
    public Task ApplyGroupAlarmsAsync(IReadOnlyList<GroupAlarmResponse> rows, bool replace=true) => Task.Run(() =>
    {
        lock(gate)
        {
            if(rows.Any(x=>x.Id==Guid.Empty||x.GroupId==Guid.Empty||x.Version<=0)||rows.Select(x=>x.Id).Distinct().Count()!=rows.Count)
                throw new GroupAlarmOperationException("O servidor retornou alarmes de grupo inválidos. O cache foi preservado.");
            InTransaction(()=>{
                var alarms=Collection();var archive=database!.GetCollection<AlarmDocument>("groupAlarmArchive");
                foreach(var row in rows)
                {
                    var active=alarms.FindById(row.Id);
                    if(active is not null&&active.GroupId is null)throw new GroupAlarmOperationException("Há um conflito de identificador. Seus alarmes pessoais foram preservados.");
                    if(active is not null&&active.GroupVersion>row.Version)continue;
                    var archived=active is null?archive.FindById(row.Id):null;
                    var original=active is not null?ToAlarm(active):archived is not null?ToAlarm(archived):null;
                    var now=(clock??TimeProvider.System).GetUtcNow().UtcDateTime;
                    var preserve=active is not null||original?.LastDeliveredOccurrenceUtc is not null?original:null;
                    var data=row.Alarm with {
                        ScheduledAtUtc=DateTimeOffset.FromUnixTimeMilliseconds(row.Alarm.ScheduledAtUtc.ToUnixTimeMilliseconds()),
                        CreatedAtUtc=DateTimeOffset.FromUnixTimeMilliseconds(row.Alarm.CreatedAtUtc.ToUnixTimeMilliseconds())
                    };
                    var alarm=AlarmSyncMapping.FromRemote(new(row.Id,row.Version,false,Guid.Empty,data),preserve,now) with {
                        GroupId=row.GroupId,GroupName=row.GroupName,GroupVersion=row.Version,CanManageGroup=row.CanManage
                    };
                    if(archived is not null&&alarm.Recurrence!=RecurrenceKind.Once&&alarm.DueAtUtc<=now)
                        alarm=alarm with {NextOccurrenceUtc=RecurrenceCalendar.NextAfter(alarm,now)};
                    alarms.Upsert(ToDocument(alarm));archive.Delete(row.Id);
                }
                if(replace)
                {
                    var ids=rows.Select(x=>x.Id).ToHashSet();
                    foreach(var removed in alarms.FindAll().Where(x=>x.GroupId is not null&&!ids.Contains(x.Id)).ToArray())
                    {archive.Upsert(removed);alarms.Delete(removed.Id);}
                }
                return true;
            });
        }
    });
    public Task RemoveGroupAlarmAsync(Alarm alarm) => Task.Run(()=>{
        lock(gate)InTransaction(()=>{
            var existing=Collection().FindById(alarm.Id);
            if(alarm.GroupId is null||existing is null||existing.GroupId!=alarm.GroupId)return false;
            database!.GetCollection<AlarmDocument>("groupAlarmArchive").Upsert(existing);
            Collection().Delete(alarm.Id);return true;
        });
    });
}
