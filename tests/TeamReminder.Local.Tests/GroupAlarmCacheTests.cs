using TeamReminder.Core;
using TeamReminder.Local;
namespace TeamReminder.Local.Tests;
public sealed class GroupAlarmCacheTests : IDisposable
{
    private readonly string directory=Path.Combine(Path.GetTempPath(),"team-reminder-group-"+Guid.NewGuid());
    private LiteDbAlarmRepository Repository()=>new(Path.Combine(directory,"cache.db"),synchronize:true);
    private static GroupAlarmResponse Row(Guid id,Guid groupId,bool manage=false,long version=1)=>new(id,groupId,"Desenvolvimento",version,manage,
        new("Grupo",DateTimeOffset.UtcNow.AddHours(2),DateTimeOffset.UtcNow,"UTC",0,0,null,null,0));
    [Fact]
    public async Task GroupSnapshotsPersistWithoutPersonalOutboxAndMembersCannotMutateThem()
    {
        var id=Guid.NewGuid();var group=Guid.NewGuid();
        using(var repo=Repository()){
            await repo.ApplyGroupAlarmsAsync([Row(id,group)]);
            var alarm=Assert.Single(await repo.ListAsync());Assert.Equal(group,alarm.GroupId);Assert.False(alarm.CanManageGroup);
            Assert.Empty(await repo.GetPendingAsync());
            Assert.False(await repo.UpdateAsync(alarm with {Title="Forbidden"}));
            Assert.False(await repo.DeleteAsync(id));
        }
        using(var repo=Repository()){var alarm=Assert.Single(await repo.ListAsync());Assert.Equal(group,alarm.GroupId);Assert.Equal(1,alarm.GroupVersion);}
    }
    [Fact]
    public async Task SuccessfulSnapshotRemovesRevokedAlarmsButKeepsPersonalAlarmsAndOfflineData()
    {
        using var repo=Repository();var id=Guid.NewGuid();var group=Guid.NewGuid();
        var personal=new Alarm(Guid.NewGuid(),"Personal",DateTime.UtcNow.AddHours(1),DateTime.UtcNow,"UTC");await repo.InsertAsync(personal);
        await repo.ApplyGroupAlarmsAsync([Row(id,group)]);
        Assert.Equal(2,(await repo.ListAsync()).Count);
        await repo.ApplyGroupAlarmsAsync([]);
        Assert.Equal(personal.Id,Assert.Single(await repo.ListAsync()).Id);
        Assert.Single(await repo.GetPendingAsync());
    }
    [Fact]
    public async Task DeliveryIsPreservedAcrossSnapshotsAndTemporaryMembershipLoss()
    {
        using var repo=Repository();var id=Guid.NewGuid();var group=Guid.NewGuid();var row=Row(id,group);
        await repo.ApplyGroupAlarmsAsync([row]);
        var alarm=Assert.Single(await repo.ListAsync());Assert.True(await repo.CompleteOccurrenceAsync(alarm,null));
        await repo.ApplyGroupAlarmsAsync([row with {CanManage=true,Version=2}]);
        alarm=Assert.Single(await repo.ListAsync());Assert.Null(alarm.DueAtUtc);Assert.True(alarm.CanManageGroup);
        await repo.ApplyGroupAlarmsAsync([]);Assert.Empty(await repo.ListAsync());
        await repo.ApplyGroupAlarmsAsync([row with {CanManage=true,Version=2}]);
        Assert.Null(Assert.Single(await repo.ListAsync()).DueAtUtc);
        Assert.Empty(await repo.GetPendingAsync());
    }
    [Fact]
    public async Task InvalidSnapshotRollsBackAndNeverOverwritesPersonalAlarm()
    {
        using var repo=Repository();var id=Guid.NewGuid();var personal=new Alarm(id,"Keep",DateTime.UtcNow.AddHours(1),DateTime.UtcNow,"UTC");
        await repo.InsertAsync(personal);
        await Assert.ThrowsAsync<GroupAlarmOperationException>(()=>repo.ApplyGroupAlarmsAsync([Row(id,Guid.NewGuid())]));
        await repo.RemoveGroupAlarmAsync(personal);
        Assert.Equal("Keep",Assert.Single(await repo.ListAsync()).Title);
    }
    public void Dispose(){if(Directory.Exists(directory))Directory.Delete(directory,true);}
}
