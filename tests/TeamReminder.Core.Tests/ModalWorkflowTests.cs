using TeamReminder.Core;

namespace TeamReminder.Core.Tests;

public class ModalWorkflowTests
{
    [Fact]
    public async Task OpeningCreateAfterMidnightUsesCurrentLocalDateAndMinute()
    {
        var clock = new FixedClock(new DateTime(2026, 10, 11, 23, 58, 45, DateTimeKind.Utc));
        var repo = new MemoryRepository();
        var zone = TimeZoneInfo.CreateCustomTimeZone("UTC+3", TimeSpan.FromHours(3), "UTC+3", "UTC+3");
        var vm = new AlarmsViewModel(new AlarmService(repo, clock), clock, () => zone);
        await vm.LoadAsync();
        vm.Title = "Rascunho";
        vm.Recurrence = RecurrenceKind.Weekly;
        vm.DayOptions[0].IsSelected = true;
        clock.Utc = new DateTime(2026, 10, 12, 21, 7, 59, DateTimeKind.Utc);
        vm.BeginCreate();
        Assert.Equal(new DateTime(2026, 10, 13), vm.SelectedDate!.Value.Date);
        Assert.Equal(new TimeSpan(0, 7, 0), vm.SelectedTime);
        Assert.Empty(vm.Title);
        Assert.Equal(RecurrenceKind.Once, vm.Recurrence);
        Assert.All(vm.DayOptions, day => Assert.False(day.IsSelected));
        Assert.Empty(repo.Alarms);
    }

    [Fact]
    public async Task SaveReturnsFalseOnValidationAndStorageFailureAndTrueOnlyAfterWrite()
    {
        var now = new DateTime(2026, 10, 11, 12, 30, 40, DateTimeKind.Utc);
        var clock = new FixedClock(now);
        var repo = new MemoryRepository();
        var vm = new AlarmsViewModel(new AlarmService(repo, clock), clock, () => TimeZoneInfo.Utc);
        await vm.LoadAsync();
        vm.BeginCreate();
        vm.Title = "Consulta";
        Assert.False(await vm.SaveAsync()); // Default current minute is not in the future.
        Assert.NotEmpty(vm.ScheduleError);
        Assert.Equal("Consulta", vm.Title);
        vm.SelectedTime = TimeSpan.FromHours(13);
        repo.FailWrite = true;
        Assert.False(await vm.SaveAsync());
        Assert.Equal("Consulta", vm.Title);
        Assert.Empty(repo.Alarms);
        repo.FailWrite = false;
        Assert.True(await vm.SaveAsync());
        Assert.Single(repo.Alarms);
    }

    [Fact]
    public async Task DiscardingEditClearsDraftWithoutChangingStoredAlarm()
    {
        var now = new DateTime(2026, 10, 11, 12, 0, 0, DateTimeKind.Utc);
        var repo = new MemoryRepository();
        var saved = new Alarm(Guid.NewGuid(), "Consulta", now.AddHours(2), now, "UTC");
        repo.Alarms.Add(saved);
        var clock = new FixedClock(now);
        var vm = new AlarmsViewModel(new AlarmService(repo, clock), clock, () => TimeZoneInfo.Utc);
        await vm.LoadAsync();
        vm.SelectedAlarm = vm.Alarms[0];
        vm.BeginEditSelected();
        Assert.Equal(TimeSpan.FromHours(14), vm.SelectedTime);
        vm.Title = "Alteração descartada";
        vm.DiscardForm();
        Assert.False(vm.IsEditing);
        Assert.Empty(vm.Title);
        Assert.Equal(saved, Assert.Single(repo.Alarms));
        Assert.Equal(0, repo.UpdateAttempts);
    }
}
