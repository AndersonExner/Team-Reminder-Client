using TeamReminder.Core;
namespace TeamReminder.Core.Tests;
public sealed class GroupAlarmFormTests
{
    private static readonly DateTime Now=new(2026,10,14,12,0,0,DateTimeKind.Utc);
    private sealed class Publisher : IGroupAlarmPublisher
    {
        public Alarm? Sent {get;private set;}
        public bool Fail {get;set;}
        public Task<Alarm> SaveAsync(Guid groupId,Alarm alarm,long version){
            Sent=alarm;if(Fail)throw new GroupAlarmOperationException("Sem conexão");
            return Task.FromResult(alarm with {GroupId=groupId,GroupName="Dev",GroupVersion=version+1,CanManageGroup=true});
        }
        public Task DeleteAsync(Alarm alarm)=>Task.CompletedTask;
    }
    [Fact]
    public async Task OnlyManagedGroupsAreChoicesAndPublishingDoesNotInsertPersonalAlarm()
    {
        var repo=new MemoryRepository();var clock=new FixedClock(Now);var vm=new AlarmsViewModel(new AlarmService(repo,clock),clock,()=>TimeZoneInfo.Utc);
        await vm.LoadAsync();var group=Guid.NewGuid();
        vm.SetDestinations([new(group,"Dev","",Guid.NewGuid(),GroupRole.Administrator,1,2),new(Guid.NewGuid(),"Read only","",Guid.NewGuid(),GroupRole.Member,1,2)]);
        Assert.Equal(2,vm.Destinations.Count);Assert.Null(vm.Destination!.GroupId);
        var publisher=new Publisher();vm.ConfigureGroupPublisher(publisher);vm.BeginCreate();vm.Destination=vm.Destinations[1];vm.Title="Shared";
        vm.SelectedDate=new DateTimeOffset(Now);vm.SelectedTime=TimeSpan.FromHours(14);
        Assert.Equal("Publicar no grupo",vm.SaveLabel);Assert.True(await vm.SaveAsync());
        Assert.Equal(group,Assert.Single(vm.Alarms).Alarm.GroupId);Assert.Empty(await repo.ListAsync());
    }
    [Fact]
    public async Task FailedPublishKeepsFormAndRetryUsesSameAlarmId()
    {
        var repo=new MemoryRepository();var clock=new FixedClock(Now);var vm=new AlarmsViewModel(new AlarmService(repo,clock),clock,()=>TimeZoneInfo.Utc);await vm.LoadAsync();
        var group=Guid.NewGuid();vm.SetDestinations([new(group,"Dev","",Guid.NewGuid(),GroupRole.Owner,1,2)]);
        var publisher=new Publisher{Fail=true};vm.ConfigureGroupPublisher(publisher);vm.Destination=vm.Destinations[1];vm.Title="Retry";
        vm.SelectedDate=new DateTimeOffset(Now);vm.SelectedTime=TimeSpan.FromHours(14);
        Assert.False(await vm.SaveAsync());var id=publisher.Sent!.Id;Assert.Equal("Retry",vm.Title);Assert.Equal("Sem conexão",vm.Message);
        publisher.Fail=false;Assert.True(await vm.SaveAsync());Assert.Equal(id,publisher.Sent!.Id);
    }
    [Fact]
    public async Task MembersCannotEditOrDeleteSharedAlarm()
    {
        var repo=new MemoryRepository();var clock=new FixedClock(Now);var alarm=new Alarm(Guid.NewGuid(),"Read only",Now.AddHours(2),Now,"UTC"){GroupId=Guid.NewGuid(),CanManageGroup=false};
        await repo.InsertAsync(alarm);var vm=new AlarmsViewModel(new AlarmService(repo,clock),clock,()=>TimeZoneInfo.Utc);await vm.LoadAsync();vm.SelectedAlarm=vm.Alarms[0];
        Assert.False(vm.CanEditSelected);Assert.False(vm.EditCommand.CanExecute(null));Assert.False(vm.DeleteCommand.CanExecute(null));
    }
}
