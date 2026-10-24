using TeamReminder.Core;

namespace TeamReminder.Core.Tests;

public class RecurrenceTests
{
    private static DateTime Utc(int month, int day, int hour = 13) => new(2026, month, day, hour, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(10, 12, 14, 10, 13)]
    [InlineData(10, 31, 14, 11, 1)]
    [InlineData(12, 31, 14, 1, 1)]
    public async Task DailyAfterTodaysTimeSchedulesTheNextDay(int month, int day, int nowHour, int nextMonth, int nextDay)
    {
        var now = Utc(month, day, nowHour);
        var repo = new MemoryRepository();
        var alarm = await new AlarmService(repo, new FixedClock(now)).CreateAsync(
            "Daily", now.Date, TimeSpan.FromHours(13), TimeZoneInfo.Utc, RecurrenceKind.Daily);
        var year = month == 12 ? 2027 : 2026;
        Assert.Equal(new DateTime(year, nextMonth, nextDay, 13, 0, 0, DateTimeKind.Utc), alarm.DueAtUtc);
    }

    [Fact]
    public async Task WeeklyMondayWednesdayFridayAdvancesAcrossWeekBoundary()
    {
        var days = WeekDays.Monday | WeekDays.Wednesday | WeekDays.Friday;
        var now = Utc(10, 11, 12); // Sunday.
        var alarm = await new AlarmService(new MemoryRepository(), new FixedClock(now))
            .CreateAsync("Treino", now.Date, TimeSpan.FromHours(13), TimeZoneInfo.Utc, RecurrenceKind.Weekly, days);
        Assert.Equal(Utc(10, 12), alarm.DueAtUtc);
        Assert.Equal(Utc(10, 14), RecurrenceCalendar.NextAfter(alarm, Utc(10, 12)));
        Assert.Equal(Utc(10, 16), RecurrenceCalendar.NextAfter(alarm, Utc(10, 14)));
        Assert.Equal(Utc(10, 19), RecurrenceCalendar.NextAfter(alarm, Utc(10, 16)));
        Assert.Equal("Seg, Qua, Sex", RecurrenceCalendar.Describe(alarm.Recurrence, alarm.Days));
        Assert.Equal("Dias úteis", RecurrenceCalendar.Describe(RecurrenceKind.Weekly, WeekDays.Monday | WeekDays.Tuesday | WeekDays.Wednesday | WeekDays.Thursday | WeekDays.Friday));
        Assert.Equal("Fins de semana", RecurrenceCalendar.Describe(RecurrenceKind.Weekly, WeekDays.Saturday | WeekDays.Sunday));
        Assert.Equal("Todos os dias", RecurrenceCalendar.Describe(RecurrenceKind.Weekly, WeekDays.All));
    }

    [Fact]
    public async Task WeeklyPassedTimeTodayChoosesNextSelectedDay()
    {
        var now = Utc(10, 12, 14);
        var alarm = await new AlarmService(new MemoryRepository(), new FixedClock(now))
            .CreateAsync("Treino", now.Date, TimeSpan.FromHours(13), TimeZoneInfo.Utc,
                RecurrenceKind.Weekly, WeekDays.Monday | WeekDays.Wednesday);
        Assert.Equal(Utc(10, 14), alarm.DueAtUtc);
    }

    [Theory]
    [InlineData(WeekDays.None)]
    [InlineData((WeekDays)128)]
    public async Task WeeklyRequiresValidSelectedDays(WeekDays days)
    {
        var now = Utc(10, 11, 12);
        var repo = new MemoryRepository();
        var ex = await Assert.ThrowsAsync<AlarmValidationException>(() => new AlarmService(repo, new FixedClock(now))
            .CreateAsync("Treino", now.Date, TimeSpan.FromHours(13), TimeZoneInfo.Utc, RecurrenceKind.Weekly, days));
        Assert.Equal("Recurrence", ex.Field);
        Assert.Empty(repo.Alarms);
    }

    [Fact]
    public async Task RecurringStartDateCannotBeBeforeToday()
    {
        var now = Utc(10, 11, 12);
        await Assert.ThrowsAsync<AlarmValidationException>(() => new AlarmService(new MemoryRepository(), new FixedClock(now))
            .CreateAsync("Daily", now.AddDays(-1), TimeSpan.FromHours(13), TimeZoneInfo.Utc, RecurrenceKind.Daily));
    }

    [Fact]
    public void UnknownOrForeignZoneIdsStillProduceNextOccurrence()
    {
        Assert.Equal(TimeZoneInfo.Local.Id, RecurrenceCalendar.ResolveZone("Mars/Olympus_Mons").Id);
        Assert.Equal(TimeZoneInfo.Local.Id, RecurrenceCalendar.ResolveZone(null).Id);
        Assert.Equal(TimeSpan.FromHours(-3), RecurrenceCalendar.ResolveZone("America/Sao_Paulo").GetUtcOffset(new DateTime(2026, 6, 1)));
        var scheduled = new DateTime(2026, 3, 7, 12, 0, 0, DateTimeKind.Utc);
        var alarm = new Alarm(Guid.NewGuid(), "Daily", scheduled, scheduled.AddDays(-1), "Mars/Olympus_Mons") { Recurrence = RecurrenceKind.Daily };
        Assert.NotNull(RecurrenceCalendar.NextAfter(alarm, scheduled));
    }

    [Fact]
    public void DailySkipsNonexistentLocalTimeAtSpringTransition()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        var scheduled = new DateTime(2026, 3, 7, 7, 30, 0, DateTimeKind.Utc); // 02:30 local.
        var alarm = new Alarm(Guid.NewGuid(), "Daily", scheduled, scheduled.AddDays(-1), zone.Id) { Recurrence = RecurrenceKind.Daily };
        Assert.Equal(new DateTime(2026, 3, 9, 6, 30, 0, DateTimeKind.Utc), RecurrenceCalendar.NextAfter(alarm, scheduled));
    }

    [Fact]
    public void AutumnTransitionUsesEarlierOccurrenceAndNeverBothOffsets()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        var scheduled = new DateTime(2026, 10, 31, 5, 30, 0, DateTimeKind.Utc); // 01:30 local.
        var alarm = new Alarm(Guid.NewGuid(), "Daily", scheduled, scheduled.AddDays(-1), zone.Id) { Recurrence = RecurrenceKind.Daily };
        var first = new DateTime(2026, 11, 1, 5, 30, 0, DateTimeKind.Utc);
        Assert.Equal(first, RecurrenceCalendar.NextAfter(alarm, scheduled));
        Assert.Equal(new DateTime(2026, 11, 2, 6, 30, 0, DateTimeKind.Utc), RecurrenceCalendar.NextAfter(alarm, first));
    }

    [Fact]
    public void WeeklySkipsInvalidDstDayUntilNextSelectedWeek()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        var scheduled = new DateTime(2026, 3, 1, 7, 30, 0, DateTimeKind.Utc);
        var alarm = new Alarm(Guid.NewGuid(), "Weekly", scheduled, scheduled.AddDays(-1), zone.Id)
        { Recurrence = RecurrenceKind.Weekly, Days = WeekDays.Sunday };
        Assert.Equal(new DateTime(2026, 3, 15, 6, 30, 0, DateTimeKind.Utc), RecurrenceCalendar.NextAfter(alarm, scheduled));
    }

    [Fact]
    public async Task EditingRearmsDeliveredAlarmWithNewRecurrence()
    {
        var now = Utc(10, 11, 12);
        var original = new Alarm(Guid.NewGuid(), "Consulta", now.AddDays(-1), now.AddDays(-2), "UTC")
        { LastDeliveredOccurrenceUtc = now.AddDays(-1) };
        var repo = new MemoryRepository();
        repo.Alarms.Add(original);
        var updated = await new AlarmService(repo, new FixedClock(now)).UpdateAsync(original, "Consulta",
            now.Date, TimeSpan.FromHours(13), TimeZoneInfo.Utc, RecurrenceKind.Weekly, WeekDays.Monday);
        Assert.Equal(original.Id, updated.Id);
        Assert.Equal(original.CreatedAtUtc, updated.CreatedAtUtc);
        Assert.Null(updated.LastDeliveredOccurrenceUtc);
        Assert.Equal(Utc(10, 12), updated.DueAtUtc);
        Assert.Equal(WeekDays.Monday, updated.Days);
    }

    [Fact]
    public async Task ViewModelDefaultsAndResetsToFiveMinutesAfterCurrentClock()
    {
        var now = Utc(10, 11, 1);
        var clock = new FixedClock(now);
        var repo = new MemoryRepository();
        var zone = TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time");
        var vm = new AlarmsViewModel(new AlarmService(repo, clock), clock, () => zone);
        Assert.Equal(new DateTime(2026, 10, 10), vm.SelectedDate!.Value.Date);
        Assert.Equal(TimeSpan.FromHours(22).Add(TimeSpan.FromMinutes(5)), vm.SelectedTime);
        Assert.Equal(RecurrenceKind.Once, vm.Recurrence);
        repo.Alarms.Add(new Alarm(Guid.NewGuid(), "Consulta", now.AddDays(2), now, "UTC"));
        await vm.LoadAsync();
        vm.SelectedAlarm = vm.Alarms[0];
        vm.BeginEditSelected();
        clock.Utc = Utc(10, 12, 12);
        vm.CancelEdit();
        Assert.Equal(new DateTime(2026, 10, 12), vm.SelectedDate!.Value.Date);
        Assert.Equal(TimeSpan.FromHours(9).Add(TimeSpan.FromMinutes(5)), vm.SelectedTime);
    }

    [Fact]
    public async Task ViewModelPersistsWeeklySelectionAndRestoresItForEditing()
    {
        var now = Utc(10, 11, 12);
        var clock = new FixedClock(now);
        var repo = new MemoryRepository();
        var vm = new AlarmsViewModel(new AlarmService(repo, clock), clock, () => TimeZoneInfo.Utc);
        await vm.LoadAsync();
        vm.Title = "Treino";
        vm.SelectedTime = TimeSpan.FromHours(13);
        vm.RecurrenceIndex = 2;
        Assert.True(vm.ShowWeekDays);
        await vm.SaveAsync();
        Assert.NotEmpty(vm.RecurrenceError);
        Assert.Empty(repo.Alarms);
        foreach (var day in vm.DayOptions) day.IsSelected = (WeekDays.Monday | WeekDays.Wednesday | WeekDays.Friday).HasFlag(day.Day);
        await vm.SaveAsync();
        var saved = Assert.Single(repo.Alarms);
        Assert.Equal(WeekDays.Monday | WeekDays.Wednesday | WeekDays.Friday, saved.Days);
        Assert.Equal(RecurrenceKind.Once, vm.Recurrence);
        vm.SelectedAlarm = Assert.Single(vm.Alarms);
        vm.BeginEditSelected();
        Assert.Equal(2, vm.RecurrenceIndex);
        Assert.Equal(3, vm.DayOptions.Count(day => day.IsSelected));
        Assert.Equal("Seg, Qua, Sex", Assert.Single(vm.Alarms).RecurrenceText);
    }
}
