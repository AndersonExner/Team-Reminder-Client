using TeamReminder.Core;

namespace TeamReminder.Core.Tests;

public class AlarmSchedulerTests
{
    private static readonly DateTime Now = new(2026, 10, 12, 13, 0, 0, DateTimeKind.Utc);
    private static Alarm Sample(DateTime? due = null) => new(Guid.NewGuid(), "Consulta", due ?? Now, Now.AddDays(-1), "UTC");

    [Fact]
    public async Task OnceDeliversAtItsTimeWithoutRepeatingOnTicksOrRestart()
    {
        var clock = new FixedClock(Now.AddSeconds(-1));
        var repo = new MemoryRepository();
        var alarm = Sample();
        repo.Alarms.Add(alarm);
        var notifier = new RecordingNotifier();
        var scheduler = new AlarmScheduler(repo, notifier, clock);
        await scheduler.TickAsync();
        Assert.Empty(notifier.Deliveries);
        clock.Utc = Now;
        await scheduler.TickAsync();
        var delivery = Assert.Single(notifier.Deliveries);
        Assert.Equal(Now, delivery.OccurrenceUtc);
        Assert.False(delivery.IsLate);
        Assert.Null(Assert.Single(repo.Alarms).DueAtUtc);
        Assert.Equal(Now, Assert.Single(repo.Alarms).LastDeliveredOccurrenceUtc);
        await scheduler.TickAsync();
        await new AlarmScheduler(repo, notifier, clock).TickAsync();
        Assert.Single(notifier.Deliveries);
    }

    [Fact]
    public async Task DailyMovesToTomorrowAtSameTime()
    {
        var repo = new MemoryRepository();
        repo.Alarms.Add(Sample() with { Recurrence = RecurrenceKind.Daily });
        var notifier = new RecordingNotifier();
        await new AlarmScheduler(repo, notifier, new FixedClock(Now)).TickAsync();
        Assert.Single(notifier.Deliveries);
        Assert.Equal(Now.AddDays(1), Assert.Single(repo.Alarms).DueAtUtc);
    }

    [Fact]
    public async Task ManyMissedDailyOccurrencesCoalesceIntoOneLateAlert()
    {
        var repo = new MemoryRepository();
        var due = Now.AddDays(-5);
        repo.Alarms.Add(Sample(due) with { Recurrence = RecurrenceKind.Daily });
        var notifier = new RecordingNotifier();
        var scheduler = new AlarmScheduler(repo, notifier, new FixedClock(Now.AddHours(1)));
        await scheduler.TickAsync();
        await scheduler.TickAsync();
        Assert.True(Assert.Single(notifier.Deliveries).IsLate);
        Assert.Equal(Now.AddDays(1), Assert.Single(repo.Alarms).DueAtUtc);
    }

    [Fact]
    public async Task FailedDeliveryDoesNotCompleteAndRetriesLater()
    {
        var repo = new MemoryRepository();
        repo.Alarms.Add(Sample());
        var notifier = new RecordingNotifier { Fail = true };
        var scheduler = new AlarmScheduler(repo, notifier, new FixedClock(Now));
        var first = await scheduler.TickAsync();
        Assert.Single(first!.Errors);
        Assert.Equal(Now, Assert.Single(repo.Alarms).DueAtUtc);
        notifier.Fail = false;
        await scheduler.TickAsync();
        Assert.Single(notifier.Deliveries);
        Assert.Null(Assert.Single(repo.Alarms).DueAtUtc);
    }

    [Fact]
    public async Task FailedReceiptRetriesWithoutPlayingAgainInSameProcess()
    {
        var repo = new MemoryRepository { FailComplete = true };
        repo.Alarms.Add(Sample());
        var notifier = new RecordingNotifier();
        var scheduler = new AlarmScheduler(repo, notifier, new FixedClock(Now));
        await scheduler.TickAsync();
        await scheduler.TickAsync();
        Assert.Single(notifier.Deliveries);
        Assert.Equal(Now, Assert.Single(repo.Alarms).DueAtUtc);
        repo.FailComplete = false;
        await scheduler.TickAsync();
        Assert.Single(notifier.Deliveries);
        Assert.Null(Assert.Single(repo.Alarms).DueAtUtc);
    }

    [Fact]
    public async Task ConcurrentTicksCannotDeliverTheSameAlarmTwice()
    {
        var repo = new MemoryRepository();
        repo.Alarms.Add(Sample());
        var notifier = new RecordingNotifier { Gate = new TaskCompletionSource() };
        var scheduler = new AlarmScheduler(repo, notifier, new FixedClock(Now));
        var first = scheduler.TickAsync();
        Assert.Null(await scheduler.TickAsync());
        notifier.Gate.SetResult();
        await first;
        Assert.Single(notifier.Deliveries);
    }

    [Fact]
    public async Task OneFailingAlarmDoesNotBlockOtherDueAlarms()
    {
        var repo = new MemoryRepository();
        var first = Sample();
        var second = Sample() with { Title = "Mercado" };
        repo.Alarms.AddRange([first, second]);
        var notifier = new RecordingNotifier { FailId = first.Id };
        var result = await new AlarmScheduler(repo, notifier, new FixedClock(Now)).TickAsync();
        Assert.Single(result!.Errors);
        Assert.Equal(second.Id, Assert.Single(notifier.Deliveries).Alarm.Id);
        Assert.Null(repo.Alarms.Single(a => a.Id == second.Id).DueAtUtc);
        Assert.NotNull(repo.Alarms.Single(a => a.Id == first.Id).DueAtUtc);
    }

    [Fact]
    public async Task CompletingOldSnapshotDoesNotOverwriteEditedOrDeletedAlarm()
    {
        var repo = new MemoryRepository();
        var original = Sample();
        var edited = original with { Title = "Novo", ScheduledAtUtc = Now.AddHours(1), NextOccurrenceUtc = Now.AddHours(1) };
        repo.Alarms.Add(edited);
        Assert.False(await repo.CompleteOccurrenceAsync(original, null));
        Assert.Equal(edited, Assert.Single(repo.Alarms));
        repo.Alarms.Clear();
        Assert.False(await repo.CompleteOccurrenceAsync(original, null));
        Assert.Empty(repo.Alarms);
    }

    [Fact]
    public async Task ProcessingAlertPreservesUnsavedFormFields()
    {
        var clock = new FixedClock(Now);
        var repo = new MemoryRepository();
        repo.Alarms.Add(Sample());
        var vm = new AlarmsViewModel(new AlarmService(repo, clock), clock, () => TimeZoneInfo.Utc);
        var notifier = new RecordingNotifier { OnDelivered = vm.ReportAlert };
        vm.ConfigureScheduler(new AlarmScheduler(repo, notifier, clock));
        await vm.LoadAsync();
        vm.SelectedAlarm = vm.Alarms[0];
        vm.BeginEditSelected();
        vm.Title = "Alteração pendente";
        vm.SelectedTime = TimeSpan.FromHours(14);
        await vm.ProcessDueAsync();
        Assert.Equal("Alteração pendente", vm.Title);
        Assert.Equal(TimeSpan.FromHours(14), vm.SelectedTime);
        Assert.True(vm.IsEditing);
        Assert.True(vm.HasAlerts);
        Assert.Equal("Disparado", Assert.Single(vm.Alarms).Status);
    }

    [Fact]
    public async Task ChangedRecordWithSameOccurrenceIsNotDeliveredAgain()
    {
        var repo = new MemoryRepository { FailComplete = true };
        var alarm = Sample();
        repo.Alarms.Add(alarm);
        var notifier = new RecordingNotifier();
        var scheduler = new AlarmScheduler(repo, notifier, new FixedClock(Now));
        await scheduler.TickAsync();
        repo.Alarms[0] = alarm with { GroupName = "Renomeado" };
        await scheduler.TickAsync();
        Assert.Single(notifier.Deliveries);
    }

    [Fact]
    public async Task AlarmFiresWhileDeleteConfirmationIsOpen()
    {
        var clock = new FixedClock(Now);
        var repo = new MemoryRepository();
        repo.Alarms.AddRange([Sample(Now.AddHours(1)), Sample()]);
        var vm = new AlarmsViewModel(new AlarmService(repo, clock), clock, () => TimeZoneInfo.Utc);
        var notifier = new RecordingNotifier();
        vm.ConfigureScheduler(new AlarmScheduler(repo, notifier, clock));
        var confirmation = new TaskCompletionSource<bool>();
        vm.ConfirmDeleteAsync = _ => confirmation.Task;
        await vm.LoadAsync();
        vm.SelectedAlarm = vm.Alarms.Single(row => row.Alarm.DueAtUtc > Now);
        var deleting = vm.DeleteSelectedAsync();
        Assert.True(vm.IsBusy);
        await vm.ProcessDueAsync();
        Assert.Single(notifier.Deliveries);
        confirmation.SetResult(false);
        await deleting;
        await vm.ProcessDueAsync();
        Assert.Contains(vm.Alarms, row => row.Status == "Disparado");
    }

    [Fact]
    public async Task AlarmFiresEvenWhenInitialLoadFailed()
    {
        var clock = new FixedClock(Now);
        var repo = new MemoryRepository { FailRead = true };
        repo.Alarms.Add(Sample());
        var vm = new AlarmsViewModel(new AlarmService(repo, clock), clock, () => TimeZoneInfo.Utc);
        var notifier = new RecordingNotifier();
        vm.ConfigureScheduler(new AlarmScheduler(repo, notifier, clock));
        await vm.LoadAsync();
        Assert.False(vm.CanEdit);
        repo.FailRead = false;
        await vm.ProcessDueAsync();
        Assert.Single(notifier.Deliveries);
    }
}

internal sealed class RecordingNotifier : IAlarmNotifier
{
    public List<AlarmDelivery> Deliveries { get; } = [];
    public bool Fail { get; set; }
    public Guid? FailId { get; set; }
    public TaskCompletionSource? Gate { get; set; }
    public Action<AlarmDelivery>? OnDelivered { get; set; }
    public async Task NotifyAsync(AlarmDelivery delivery)
    {
        if (Gate is not null) await Gate.Task;
        if (Fail || FailId == delivery.Alarm.Id) throw new IOException("Simulated notification failure");
        Deliveries.Add(delivery);
        OnDelivered?.Invoke(delivery);
    }
}
