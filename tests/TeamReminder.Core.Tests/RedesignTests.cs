using TeamReminder.Core;

namespace TeamReminder.Core.Tests;

public class RedesignTests
{
    private static readonly DateTime Now = new(2026, 10, 24, 15, 0, 0, DateTimeKind.Utc); // Saturday.
    private static Alarm At(string title, DateTime due) => new(Guid.NewGuid(), title, due, Now.AddDays(-1), "UTC") { NextOccurrenceUtc = due };

    [Fact]
    public void DayLabelsAndCountdownReadNaturally()
    {
        var today = Now.Date;
        Assert.Equal("Hoje", AlarmRow.DayLabel(today, today));
        Assert.Equal("Amanhã", AlarmRow.DayLabel(today.AddDays(1), today));
        Assert.Equal("Ontem", AlarmRow.DayLabel(today.AddDays(-1), today));
        Assert.Equal("Terça-feira", AlarmRow.DayLabel(today.AddDays(3), today));
        Assert.Equal("Sex., 13/11", AlarmRow.DayLabel(new DateTime(2026, 11, 13), today));
        Assert.Equal("15/01/2027", AlarmRow.DayLabel(new DateTime(2027, 1, 15), today));
        Assert.Equal("Agora", AlarmRow.Until(TimeSpan.Zero));
        Assert.Equal("Em menos de 1 min", AlarmRow.Until(TimeSpan.FromSeconds(20)));
        Assert.Equal("Em 5 min", AlarmRow.Until(TimeSpan.FromMinutes(4.2)));
        Assert.Equal("Em 2 h", AlarmRow.Until(TimeSpan.FromHours(2)));
        Assert.Equal("Em 2 h 05 min", AlarmRow.Until(TimeSpan.FromMinutes(125)));
        Assert.Equal("Em 3 dias", AlarmRow.Until(TimeSpan.FromDays(3)));
    }

    [Fact]
    public async Task SectionsGroupByDayAndHomeSummaryFindsNextAlarm()
    {
        var clock = new FixedClock(Now);
        var repo = new MemoryRepository();
        var delivered = new Alarm(Guid.NewGuid(), "Café", Now.AddHours(-1), Now.AddDays(-1), "UTC") { LastDeliveredOccurrenceUtc = Now.AddHours(-1) };
        repo.Alarms.AddRange([At("Ponto", Now.AddHours(3)), At("Daily", Now.AddMinutes(30)), At("Consulta", Now.AddDays(1)),
            At("Revisão", Now.AddDays(4)), At("Certificado", Now.AddDays(30)), delivered]);
        var vm = new AlarmsViewModel(new AlarmService(repo, clock), clock, () => TimeZoneInfo.Utc);
        await vm.LoadAsync();
        Assert.Equal([AlarmSection.Today, AlarmSection.Tomorrow, AlarmSection.Week, AlarmSection.Later, AlarmSection.Delivered],
            vm.Sections.Select(section => section.Title));
        Assert.Equal(["Daily", "Ponto"], vm.Sections[0].Rows.Select(row => row.Title));
        Assert.Equal("Daily", vm.NextAlarm!.Title);
        Assert.Equal("Em 30 min", vm.NextAlarm.Countdown);
        Assert.Equal((2, 5), (vm.TodayCount, vm.ActiveCount));
        Assert.Equal("2 alarmes hoje · 5 ativos", vm.SummaryText);
        Assert.Equal("Boa tarde", vm.Greeting);
        vm.UserName = "Anderson Exner";
        Assert.Equal("Boa tarde, Anderson", vm.Greeting);
        Assert.Equal("Sábado, 24 de outubro", vm.TodayText);
    }

    [Fact]
    public async Task SearchFiltersSectionsWithoutTouchingTheAlarmList()
    {
        var clock = new FixedClock(Now);
        var repo = new MemoryRepository();
        repo.Alarms.AddRange([At("Daily da equipe", Now.AddHours(1)), At("Consulta", Now.AddDays(1))]);
        var vm = new AlarmsViewModel(new AlarmService(repo, clock), clock, () => TimeZoneInfo.Utc);
        await vm.LoadAsync();
        vm.FilterText = "daily";
        Assert.Equal("Daily da equipe", Assert.Single(Assert.Single(vm.Sections).Rows).Title);
        Assert.Equal(2, vm.Alarms.Count);
        vm.FilterText = "inexistente";
        Assert.Empty(vm.Sections);
        Assert.True(vm.NoMatches);
    }

    [Fact]
    public async Task QuickSchedulesFillDateAndTimeInTheLocalZone()
    {
        var clock = new FixedClock(Now.AddSeconds(30));
        var vm = new AlarmsViewModel(new AlarmService(new MemoryRepository(), clock), clock, () => TimeZoneInfo.Utc);
        await vm.LoadAsync();
        vm.ApplyQuickSchedule(TimeSpan.FromMinutes(15));
        Assert.Equal(Now.Date, vm.SelectedDate!.Value.Date);
        Assert.Equal(new TimeSpan(15, 16, 0), vm.SelectedTime);
        vm.ApplyTomorrowAt(9);
        Assert.Equal(Now.Date.AddDays(1), vm.SelectedDate!.Value.Date);
        Assert.Equal(TimeSpan.FromHours(9), vm.SelectedTime);
    }

    private sealed class MemorySnoozeStore : ISnoozeStore
    {
        public IReadOnlyList<SnoozeEntry> Saved { get; private set; } = [];
        public Task<IReadOnlyList<SnoozeEntry>> LoadAsync() => Task.FromResult(Saved);
        public Task SaveAsync(IReadOnlyList<SnoozeEntry> entries) { Saved = entries; return Task.CompletedTask; }
    }

    private sealed class MovingClock(DateTime start) : TimeProvider
    {
        public DateTime Utc { get; set; } = start;
        public override DateTimeOffset GetUtcNow() => new(Utc, TimeSpan.Zero);
    }

    [Fact]
    public async Task SnoozedAlarmRingsAgainOnceAndSurvivesRestart()
    {
        var clock = new MovingClock(Now);
        var repo = new MemoryRepository();
        var alarm = At("Daily", Now);
        repo.Alarms.Add(alarm);
        var store = new MemorySnoozeStore();
        var notifier = new RecordingNotifier();
        var until = await new SnoozeScheduler(repo, store, notifier, clock).SnoozeAsync(alarm.Id, Now, TimeSpan.FromMinutes(10));
        Assert.Equal(Now.AddMinutes(10), until);

        // A new instance (app restart) reads the persisted snooze.
        var restarted = new SnoozeScheduler(repo, store, notifier, clock);
        clock.Utc = Now.AddMinutes(9);
        await restarted.TickAsync();
        Assert.Empty(notifier.Deliveries);
        clock.Utc = Now.AddMinutes(10);
        await restarted.TickAsync();
        await restarted.TickAsync();
        var delivery = Assert.Single(notifier.Deliveries);
        Assert.True(delivery.IsSnoozed);
        Assert.Equal(Now, delivery.OccurrenceUtc);
        Assert.Empty(store.Saved);
    }

    [Fact]
    public async Task SnoozeOfDeletedAlarmIsDroppedAndFailedDeliveryRetries()
    {
        var clock = new MovingClock(Now);
        var repo = new MemoryRepository();
        var kept = At("Ponto", Now);
        repo.Alarms.Add(kept);
        var store = new MemorySnoozeStore();
        var notifier = new RecordingNotifier { Fail = true };
        var scheduler = new SnoozeScheduler(repo, store, notifier, clock);
        await scheduler.SnoozeAsync(Guid.NewGuid(), Now, TimeSpan.FromMinutes(5));
        await scheduler.SnoozeAsync(kept.Id, Now, TimeSpan.FromMinutes(5));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => scheduler.SnoozeAsync(kept.Id, Now, TimeSpan.Zero));
        clock.Utc = Now.AddMinutes(5);
        Assert.Single(await scheduler.TickAsync());
        Assert.Equal(kept.Id, Assert.Single(store.Saved).AlarmId);
        notifier.Fail = false;
        Assert.Empty(await scheduler.TickAsync());
        Assert.Single(notifier.Deliveries);
        Assert.Empty(await scheduler.PendingAsync());
    }
}
