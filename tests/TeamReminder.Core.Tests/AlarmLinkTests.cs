using TeamReminder.Core;

namespace TeamReminder.Core.Tests;

public class AlarmLinkTests
{
    private static readonly DateTime Now = new(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("ftp://example.com")]
    [InlineData("file:///C:/test.exe")]
    [InlineData("javascript:alert(1)")]
    [InlineData("mailto:test@example.com")]
    [InlineData("example.com/ponto")]
    [InlineData("//example.com")]
    [InlineData("https://")]
    [InlineData("https://user:password@example.com")]
    [InlineData("https://example.com/a\nb")]
    public async Task InvalidLinkDoesNotWrite(string url)
    {
        var repo = new MemoryRepository(); var service = new AlarmService(repo, new FixedClock(Now));
        var error = await Assert.ThrowsAsync<AlarmValidationException>(() =>
            service.CreateAsync("Ponto", Now.Date, TimeSpan.FromHours(13), TimeZoneInfo.Utc, linkUrl: url));
        Assert.Equal("Action", error.Field); Assert.Empty(repo.Alarms);
    }

    [Fact]
    public void OptionalFieldsAndLimitsAreValidated()
    {
        Assert.Null(AlarmLink.Create(" ", null));
        Assert.Equal("Abrir link", AlarmLink.Create(" HTTPS://Example.com/ponto?a=1&b=2#fim ", "")!.Label);
        Assert.Throws<AlarmValidationException>(() => AlarmLink.Create(null, "Ponto"));
        Assert.Throws<AlarmValidationException>(() => AlarmLink.Create("https://example.com", new string('x', 41)));
        Assert.Throws<AlarmValidationException>(() => AlarmLink.Create("https://example.com", "Bater\nponto"));
        Assert.Throws<AlarmValidationException>(() => AlarmLink.Create("https://example.com/" + new string('x', 2048), "Ponto"));
        Assert.NotNull(AlarmLink.Create("http://intranet/ponto", new string('x', 40)));
    }

    [Fact]
    public async Task EditCancelRemoveAndFailedSavePreserveExpectedDraftAndRecord()
    {
        var repo = new MemoryRepository(); var clock = new FixedClock(Now);
        var vm = new AlarmsViewModel(new AlarmService(repo, clock), clock, () => TimeZoneInfo.Utc);
        await vm.LoadAsync(); vm.BeginCreate(); vm.Title = "Ponto"; vm.SelectedTime = TimeSpan.FromHours(13);
        vm.ActionUrl = "bad"; vm.ActionLabel = "Bater ponto";
        Assert.False(await vm.SaveAsync()); Assert.NotEmpty(vm.ActionError); Assert.Equal("bad", vm.ActionUrl);
        vm.ActionUrl = "https://example.com/ponto"; Assert.True(await vm.SaveAsync());
        var first = Assert.Single(repo.Alarms);
        Assert.Equal("Bater ponto", first.Link!.Label);
        Assert.Empty(vm.ActionUrl); Assert.Empty(vm.ActionLabel);
        vm.SelectedAlarm = vm.Alarms[0]; vm.BeginEditSelected();
        Assert.Equal(first.Link.Url, vm.ActionUrl);
        vm.ActionUrl = "https://example.com/new"; vm.DiscardForm();
        Assert.Equal(first, Assert.Single(repo.Alarms));
        vm.BeginEditSelected(); vm.ActionUrl = "https://example.com/new"; repo.FailUpdate = true;
        Assert.False(await vm.SaveAsync()); Assert.Equal("https://example.com/new", vm.ActionUrl);
        repo.FailUpdate = false; Assert.True(await vm.SaveAsync());
        Assert.Equal("https://example.com/new", Assert.Single(repo.Alarms).Link!.Url);
        vm.BeginEditSelected(); vm.ActionUrl = vm.ActionLabel = "";
        Assert.True(await vm.SaveAsync()); Assert.Null(Assert.Single(repo.Alarms).Link);
    }

    [Fact]
    public async Task LoadingSavingAndReportingNeverLaunchAndClickOnlyLaunchesValidatedSnapshot()
    {
        var repo = new MemoryRepository(); var clock = new FixedClock(Now);
        var vm = new AlarmsViewModel(new AlarmService(repo, clock), clock, () => TimeZoneInfo.Utc);
        var launched = new List<Uri>();
        vm.LaunchLinkAsync = uri => { launched.Add(uri); return Task.FromResult(true); };
        await vm.LoadAsync(); vm.BeginCreate(); vm.Title = "Ponto"; vm.SelectedTime = TimeSpan.FromHours(13);
        vm.ActionUrl = "https://example.com/ponto?x=1&y=2#fim"; Assert.True(await vm.SaveAsync());
        var alarm = Assert.Single(repo.Alarms);
        vm.ReportAlert(new AlarmDelivery(alarm, alarm.ScheduledAtUtc, false)); await vm.LoadAsync();
        Assert.Empty(launched);
        Assert.Equal(alarm, Assert.Single(vm.ReceivedAlarms).Alarm);
        await vm.OpenActionAsync(alarm);
        Assert.Equal(alarm.Link!.Url, Assert.Single(launched).AbsoluteUri);
        Assert.Equal(alarm, Assert.Single(repo.Alarms));
        await vm.OpenActionAsync(alarm with { Link = new AlarmLink("file:///C:/bad.exe", "Bad") });
        Assert.Single(launched); Assert.Contains("válido", vm.Message);
        vm.LaunchLinkAsync = _ => Task.FromResult(false); await vm.OpenActionAsync(alarm);
        Assert.Contains("Não foi possível", vm.Message);
        vm.LaunchLinkAsync = _ => throw new IOException(); await vm.OpenActionAsync(alarm);
        Assert.Equal(alarm, Assert.Single(repo.Alarms)); Assert.Contains("Não foi possível", vm.Message);
        for (var i = 0; i < 12; i++) vm.ReportAlert(new AlarmDelivery(alarm, alarm.ScheduledAtUtc, false));
        Assert.Equal(10, vm.ReceivedAlarms.Count);
        vm.DismissAlertsCommand.Execute(null); Assert.Empty(vm.ReceivedAlarms); Assert.False(vm.HasAlerts);
    }
}
