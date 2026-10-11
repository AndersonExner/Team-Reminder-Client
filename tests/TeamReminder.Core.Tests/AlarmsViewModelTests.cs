using TeamReminder.Core;

namespace TeamReminder.Core.Tests;

public class AlarmsViewModelTests
{
    private static readonly DateTime Now = new(2026, 10, 11, 12, 0, 0, DateTimeKind.Utc);
    private static AlarmsViewModel Create(MemoryRepository repo, FixedClock? clock = null)
    {
        clock ??= new FixedClock(Now);
        return new AlarmsViewModel(new AlarmService(repo, clock), clock, () => TimeZoneInfo.Utc);
    }
    private static void Fill(AlarmsViewModel vm)
    {
        vm.Title = "Consulta";
        vm.SelectedDate = new DateTimeOffset(Now);
        vm.SelectedTime = TimeSpan.FromHours(13);
    }

    [Fact]
    public async Task SaveUpdatesListAndClearsFieldsOnlyAfterWrite()
    {
        var repo = new MemoryRepository { WriteGate = new TaskCompletionSource() };
        var vm = Create(repo);
        await vm.LoadAsync();
        Fill(vm);
        var save = vm.SaveAsync();
        Assert.True(vm.IsBusy);
        Assert.False(vm.SaveCommand.CanExecute(null));
        Assert.Empty(vm.Alarms);
        Assert.Equal("Consulta", vm.Title);
        repo.WriteGate.SetResult();
        await save;
        Assert.Single(vm.Alarms);
        Assert.Equal("", vm.Title);
        Assert.Equal(Now.Date, vm.SelectedDate!.Value.Date);
        Assert.Equal(Now.TimeOfDay, vm.SelectedTime);
        Assert.Equal("Alarme salvo.", vm.Message);
    }

    [Fact]
    public async Task ConcurrentSubmissionsWriteOnce()
    {
        var repo = new MemoryRepository { WriteGate = new TaskCompletionSource() };
        var vm = Create(repo);
        await vm.LoadAsync();
        Fill(vm);
        var first = vm.SaveAsync();
        await vm.SaveAsync();
        Assert.Equal(1, repo.WriteAttempts);
        repo.WriteGate.SetResult();
        await first;
        Assert.Single(vm.Alarms);
    }

    [Fact]
    public async Task WriteFailurePreservesFieldsAndAllowsRetry()
    {
        var repo = new MemoryRepository { FailWrite = true };
        var vm = Create(repo);
        await vm.LoadAsync();
        Fill(vm);
        var date = vm.SelectedDate;
        var time = vm.SelectedTime;
        await vm.SaveAsync();
        Assert.Empty(vm.Alarms);
        Assert.Equal("Consulta", vm.Title);
        Assert.Equal(date, vm.SelectedDate);
        Assert.Equal(time, vm.SelectedTime);
        Assert.Contains("não foi salvo", vm.Message);
        Assert.True(vm.SaveCommand.CanExecute(null));
        repo.FailWrite = false;
        await vm.SaveAsync();
        Assert.Single(vm.Alarms);
    }

    [Fact]
    public async Task ReadFailureIsNotAnEmptyStateAndCanRetry()
    {
        var repo = new MemoryRepository { FailRead = true };
        var vm = Create(repo);
        await vm.LoadAsync();
        Assert.Contains("não foi possível", vm.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("", vm.EmptyMessage);
        Assert.False(vm.CanEdit);
        Assert.False(vm.SaveCommand.CanExecute(null));
        repo.FailRead = false;
        await vm.LoadAsync();
        Assert.Equal("", vm.Message);
        Assert.Equal("Crie seu primeiro alarme.", vm.EmptyMessage);
        Assert.True(vm.CanEdit);
    }

    [Fact]
    public async Task ValidationFailureKeepsFieldsAndShowsFieldError()
    {
        var repo = new MemoryRepository();
        var vm = Create(repo);
        await vm.LoadAsync();
        Fill(vm);
        vm.Title = " ";
        await vm.SaveAsync();
        Assert.NotEmpty(vm.TitleError);
        Assert.Equal(" ", vm.Title);
        Assert.NotNull(vm.SelectedDate);
        Assert.Equal(0, repo.WriteAttempts);
    }

    [Fact]
    public async Task ListSortsWithTieBreakersAndKeepsPastAlarms()
    {
        var repo = new MemoryRepository();
        var past = new Alarm(Guid.Parse("00000000-0000-0000-0000-000000000001"), "Passado", Now.AddHours(-1), Now, "UTC");
        var future = new Alarm(Guid.Parse("00000000-0000-0000-0000-000000000003"), "Futuro", Now.AddHours(1), Now, "UTC");
        var tied = future with { Id = Guid.Parse("00000000-0000-0000-0000-000000000002") };
        var earlierCreation = future with { Id = Guid.NewGuid(), CreatedAtUtc = Now.AddDays(-1) };
        repo.Alarms.AddRange([future, tied, past, earlierCreation]);
        var vm = Create(repo);
        await vm.LoadAsync();
        Assert.Equal(new[] { past.Id, earlierCreation.Id, tied.Id, future.Id }, vm.Alarms.Select(r => r.Alarm.Id));
        Assert.Equal("Aguardando disparo", vm.Alarms[0].Status);
        Assert.Equal("Agendado", vm.Alarms[1].Status);
    }

    [Fact]
    public async Task TimeRefreshRaisesStatusChangeWhenAlarmBecomesPast()
    {
        var clock = new FixedClock(Now);
        var repo = new MemoryRepository();
        repo.Alarms.Add(new Alarm(Guid.NewGuid(), "Consulta", Now.AddHours(1), Now, "UTC"));
        var vm = Create(repo, clock);
        await vm.LoadAsync();
        var row = Assert.Single(vm.Alarms);
        var notified = false;
        row.PropertyChanged += (_, e) => notified |= e.PropertyName == nameof(AlarmRow.Status);
        clock.Utc = Now.AddHours(1);
        vm.RefreshTime();
        Assert.True(notified);
        Assert.Equal("Aguardando disparo", row.Status);
    }
}
