using TeamReminder.Core;

namespace TeamReminder.Core.Tests;

public class AlarmSoundTests
{
    private static readonly DateTime Now = new(2026, 10, 11, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task InvalidCustomSoundDoesNotSaveAndPreservesDraftForRetry()
    {
        var repo = new MemoryRepository();
        var clock = new FixedClock(Now);
        var vm = new AlarmsViewModel(new AlarmService(repo, clock), clock, () => TimeZoneInfo.Utc);
        await vm.LoadAsync(); vm.BeginCreate(); vm.Title = "Consulta";
        vm.SelectedTime = TimeSpan.FromHours(13);
        vm.Sound = new AlarmSound(AlarmSoundKind.Custom);
        Assert.False(await vm.SaveAsync());
        Assert.NotEmpty(vm.SoundError); Assert.Empty(repo.Alarms);
        Assert.Equal("Consulta", vm.Title);
        var selected = new AlarmSound(AlarmSoundKind.Custom, Guid.NewGuid(), "meu som.mp3");
        vm.Sound = selected;
        Assert.True(await vm.SaveAsync());
        Assert.Equal(selected, Assert.Single(repo.Alarms).Sound);
        Assert.Equal(AlarmSound.Default, vm.Sound);
    }

    [Fact]
    public async Task EditRestoresSoundAndCancelDoesNotChangeIt()
    {
        var repo = new MemoryRepository(); var clock = new FixedClock(Now);
        var service = new AlarmService(repo, clock);
        var selected = new AlarmSound(AlarmSoundKind.Custom, Guid.NewGuid(), "som.wav");
        await service.CreateAsync("Consulta", Now.Date, TimeSpan.FromHours(13), TimeZoneInfo.Utc, sound: selected);
        var vm = new AlarmsViewModel(service, clock, () => TimeZoneInfo.Utc);
        await vm.LoadAsync(); vm.SelectedAlarm = vm.Alarms[0]; vm.BeginEditSelected();
        Assert.Equal(selected, vm.Sound);
        vm.Sound = new AlarmSound(AlarmSoundKind.Silent); vm.DiscardForm();
        Assert.Equal(selected, Assert.Single(repo.Alarms).Sound);
        vm.BeginEditSelected(); vm.Sound = new AlarmSound(AlarmSoundKind.Critical);
        Assert.True(await vm.SaveAsync());
        Assert.Equal(AlarmSoundKind.Critical, Assert.Single(repo.Alarms).Sound.Kind);
    }

    [Fact]
    public async Task InvalidSoundKindAndUnexpectedFileReferenceAreRejected()
    {
        var repo = new MemoryRepository(); var service = new AlarmService(repo, new FixedClock(Now));
        foreach (var sound in new[] { new AlarmSound((AlarmSoundKind)99),
            new AlarmSound(AlarmSoundKind.Custom, Guid.Empty, "som.mp3"),
            new AlarmSound(AlarmSoundKind.Silent, Guid.NewGuid(), "som.mp3") })
            await Assert.ThrowsAsync<AlarmValidationException>(() =>
                service.CreateAsync("Consulta", Now.Date, TimeSpan.FromHours(13), TimeZoneInfo.Utc, sound: sound));
        Assert.Empty(repo.Alarms);
    }
}
