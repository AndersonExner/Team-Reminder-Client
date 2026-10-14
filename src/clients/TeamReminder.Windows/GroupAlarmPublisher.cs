using TeamReminder.Core;
using TeamReminder.Local;
namespace TeamReminder.Windows;
public sealed class GroupAlarmPublisher(AccountControl account,AccountAlarmRepository repository) : IGroupAlarmPublisher
{
    public async Task<Alarm> SaveAsync(Guid groupId,Alarm alarm,long expectedVersion)
    {
        if(alarm.CacheScope!=repository.CurrentScope)throw new GroupAlarmOperationException("A conta mudou. Abra o formulário novamente.");
        var cache=repository.Current;
        try
        {
            var mutationId=expectedVersion==0?alarm.Id:Guid.NewGuid();
            var response=await account.ExecuteAuthenticatedAsync((api,token)=>api.SaveGroupAlarmAsync(groupId,alarm.Id,
                new(expectedVersion,mutationId,false,AlarmSyncMapping.Definition(alarm)),token),deferInvalidation:true);
            await cache.ApplyGroupAlarmsAsync([response],replace:false);
            return AlarmSyncMapping.FromRemote(new(response.Id,response.Version,false,mutationId,response.Alarm),alarm,DateTime.UtcNow) with {
                CacheScope=alarm.CacheScope,GroupId=groupId,GroupName=response.GroupName,GroupVersion=response.Version,CanManageGroup=response.CanManage
            };
        }
        catch(AccountApiException ex){throw new GroupAlarmOperationException(ex.Message);}
        catch(HttpRequestException){throw new GroupAlarmOperationException("Não foi possível conectar. Publicar no grupo exige internet. Tente novamente.");}
        catch(TaskCanceledException){throw new GroupAlarmOperationException("O servidor demorou para responder. Tente novamente; o mesmo alarme será usado para evitar duplicação.");}
    }
    public async Task DeleteAsync(Alarm alarm)
    {
        if(alarm.CacheScope!=repository.CurrentScope)throw new GroupAlarmOperationException("A conta mudou. Atualize a lista.");
        var cache=repository.Current;
        try{
            await account.ExecuteAuthenticatedAsync((api,token)=>api.DeleteGroupAlarmAsync(alarm.GroupId!.Value,alarm.Id,alarm.GroupVersion,Guid.NewGuid(),token),deferInvalidation:true);
            await cache.RemoveGroupAlarmAsync(alarm);
        }
        catch(AccountApiException ex){throw new GroupAlarmOperationException(ex.Message);}
        catch(HttpRequestException){throw new GroupAlarmOperationException("Excluir um alarme de grupo exige conexão. Tente novamente.");}
    }
}
