using TeamReminder.Core;

namespace TeamReminder.Core.Tests;

public class EditDeleteTests
{
    private static readonly DateTime Now = new(2026, 10, 11, 12, 0, 0, DateTimeKind.Utc);
    private static Alarm Sample(int hour = 13) => new(Guid.NewGuid(), "Consulta", Now.Date.AddHours(hour), Now.AddDays(-1), "UTC");

    private static async Task<AlarmsViewModel> Loaded(MemoryRepository repo)
    {
        var clock = new FixedClock(Now);
        var vm = new AlarmsViewModel(new AlarmService(repo, clock), clock, () => TimeZoneInfo.Utc);
        await vm.LoadAsync();
        return vm;
    }

    [Fact]
    public async Task UpdateKeepsIdentityAndCreationWhileNormalizingAndConverting()
    {
        var repo = new MemoryRepository();
        var original = Sample();
        repo.Alarms.Add(original);
        var zone = TimeZoneInfo.CreateCustomTimeZone("TestMinus3", TimeSpan.FromHours(-3), "Test", "Test");
        var updated = await new AlarmService(repo, new FixedClock(Now))
            .UpdateAsync(original, "  Mercado  ", Now.Date, TimeSpan.FromHours(14), zone);
        Assert.Equal(original.Id, updated.Id);
        Assert.Equal(original.CreatedAtUtc, updated.CreatedAtUtc);
        Assert.Equal("Mercado", updated.Title);
        Assert.Equal(Now.Date.AddHours(17), updated.ScheduledAtUtc);
        Assert.Equal(zone.Id, updated.TimeZoneId);
        Assert.Equal(updated, Assert.Single(repo.Alarms));
    }

    [Theory]
    [InlineData("", 14)]
    [InlineData(" ", 14)]
    [InlineData("Consulta", 11)]
    [InlineData("Consulta", 12)]
    public async Task InvalidEditDoesNotWrite(string title, int hour)
    {
        var repo = new MemoryRepository();
        var original = Sample();
        repo.Alarms.Add(original);
        await Assert.ThrowsAsync<AlarmValidationException>(() => new AlarmService(repo, new FixedClock(Now))
            .UpdateAsync(original, title, Now.Date, TimeSpan.FromHours(hour), TimeZoneInfo.Utc));
        Assert.Equal(original, Assert.Single(repo.Alarms));
        Assert.Equal(0, repo.UpdateAttempts);
    }

    [Fact]
    public async Task MissingIdIsNeverRecreated()
    {
        var repo = new MemoryRepository();
        var original = Sample();
        var service = new AlarmService(repo, new FixedClock(Now));
        await Assert.ThrowsAsync<AlarmNotFoundException>(() => service.UpdateAsync(original,
            "Consulta", Now.Date, TimeSpan.FromHours(14), TimeZoneInfo.Utc));
        await Assert.ThrowsAsync<AlarmNotFoundException>(() => service.DeleteAsync(original.Id));
        Assert.Empty(repo.Alarms);
        Assert.Equal(0, repo.WriteAttempts);
    }

    [Fact]
    public async Task EditAndDeleteCommandsRequireSelection()
    {
        var repo = new MemoryRepository();
        repo.Alarms.Add(Sample());
        var vm = await Loaded(repo);
        Assert.False(vm.EditCommand.CanExecute(null));
        Assert.False(vm.DeleteCommand.CanExecute(null));
        vm.SelectedAlarm = vm.Alarms[0];
        Assert.True(vm.EditCommand.CanExecute(null));
        Assert.True(vm.DeleteCommand.CanExecute(null));
    }

    [Fact]
    public async Task BeginEditUsesTheCurrentZoneAndKeepsCreation()
    {
        var repo = new MemoryRepository();
        var original = Sample(17);
        repo.Alarms.Add(original);
        var clock = new FixedClock(Now);
        var zone = TimeZoneInfo.CreateCustomTimeZone("TestMinus3", TimeSpan.FromHours(-3), "Test", "Test");
        var vm = new AlarmsViewModel(new AlarmService(repo, clock), clock, () => zone);
        await vm.LoadAsync();
        vm.SelectedAlarm = vm.Alarms[0];
        vm.BeginEditSelected();
        Assert.True(vm.IsEditing);
        Assert.Equal("Editar alarme", vm.FormTitle);
        Assert.Equal("Salvar alterações", vm.SaveLabel);
        Assert.Equal(original.Title, vm.Title);
        Assert.Equal(Now.Date, vm.SelectedDate!.Value.Date);
        Assert.Equal(TimeSpan.FromHours(14), vm.SelectedTime);
        Assert.Equal(TimeSpan.FromHours(-3), vm.SelectedDate.Value.Offset);
    }

    [Fact]
    public async Task EditUpdatesTheSameItemAndReordersWithoutDuplicates()
    {
        var repo = new MemoryRepository();
        var first = Sample(13);
        var second = Sample(14);
        repo.Alarms.AddRange([first, second]);
        var vm = await Loaded(repo);
        vm.SelectedAlarm = vm.Alarms[0];
        vm.BeginEditSelected();
        vm.Title = "Consulta alterada";
        vm.SelectedTime = TimeSpan.FromHours(15);
        await vm.SaveAsync();
        Assert.Equal(new[] { second.Id, first.Id }, vm.Alarms.Select(a => a.Alarm.Id));
        Assert.Equal("Consulta alterada", vm.Alarms[1].Title);
        Assert.Equal(first.CreatedAtUtc, vm.Alarms[1].Alarm.CreatedAtUtc);
        Assert.Equal(2, repo.Alarms.Count);
        Assert.False(vm.IsEditing);
        Assert.Equal("", vm.Title);
        Assert.Equal("Alarme atualizado.", vm.Message);
    }

    [Fact]
    public async Task CancelEditLeavesTheDatabaseUnchanged()
    {
        var repo = new MemoryRepository();
        var original = Sample();
        repo.Alarms.Add(original);
        var vm = await Loaded(repo);
        vm.SelectedAlarm = vm.Alarms[0];
        vm.BeginEditSelected();
        vm.Title = "Não salvar";
        vm.CancelEdit();
        Assert.Equal(original, Assert.Single(repo.Alarms));
        Assert.Equal(0, repo.UpdateAttempts);
        Assert.False(vm.IsEditing);
        Assert.Equal("", vm.Title);
        Assert.Equal(Now.Date, vm.SelectedDate!.Value.Date);
        Assert.Null(vm.SelectedTime);
    }

    [Fact]
    public async Task InvalidEditKeepsFormAndOriginalRow()
    {
        var repo = new MemoryRepository();
        var original = Sample();
        repo.Alarms.Add(original);
        var vm = await Loaded(repo);
        vm.SelectedAlarm = vm.Alarms[0];
        vm.BeginEditSelected();
        vm.Title = " ";
        await vm.SaveAsync();
        Assert.NotEmpty(vm.TitleError);
        Assert.True(vm.IsEditing);
        Assert.Equal(" ", vm.Title);
        Assert.Equal(original, Assert.Single(vm.Alarms).Alarm);
        Assert.Equal(0, repo.UpdateAttempts);
    }

    [Fact]
    public async Task FailedEditKeepsFormAndAllowsRetry()
    {
        var repo = new MemoryRepository { FailUpdate = true };
        var original = Sample();
        repo.Alarms.Add(original);
        var vm = await Loaded(repo);
        vm.SelectedAlarm = vm.Alarms[0];
        vm.BeginEditSelected();
        vm.Title = "Alteração";
        await vm.SaveAsync();
        Assert.Equal(original, Assert.Single(vm.Alarms).Alarm);
        Assert.Equal("Alteração", vm.Title);
        Assert.True(vm.IsEditing);
        Assert.Contains("não foi salvo", vm.Message);
        repo.FailUpdate = false;
        await vm.SaveAsync();
        Assert.Equal("Alteração", Assert.Single(vm.Alarms).Title);
        Assert.Equal(original.Id, Assert.Single(vm.Alarms).Alarm.Id);
    }

    [Fact]
    public async Task RepeatedEditWaitsForOneWriteBeforeUpdatingTheList()
    {
        var repo = new MemoryRepository { UpdateGate = new TaskCompletionSource() };
        var original = Sample();
        repo.Alarms.Add(original);
        var vm = await Loaded(repo);
        vm.SelectedAlarm = vm.Alarms[0];
        vm.BeginEditSelected();
        vm.Title = "Alteração";
        var save = vm.SaveAsync();
        await vm.SaveAsync();
        Assert.Equal(1, repo.UpdateAttempts);
        Assert.Equal(original, Assert.Single(vm.Alarms).Alarm);
        Assert.False(vm.CanEdit);
        Assert.False(vm.DeleteCommand.CanExecute(null));
        repo.UpdateGate.SetResult();
        await save;
        Assert.Equal("Alteração", Assert.Single(vm.Alarms).Title);
    }

    [Fact]
    public async Task CancellationOfDeletionPreservesEditingAndDoesNotDelete()
    {
        var repo = new MemoryRepository();
        var original = Sample();
        repo.Alarms.Add(original);
        var vm = await Loaded(repo);
        vm.SelectedAlarm = vm.Alarms[0];
        vm.BeginEditSelected();
        vm.Title = "Alteração não salva";
        Alarm? shown = null;
        vm.ConfirmDeleteAsync = alarm => { shown = alarm; return Task.FromResult(false); };
        await vm.DeleteSelectedAsync();
        Assert.Equal(original, shown);
        Assert.Equal(original, Assert.Single(vm.Alarms).Alarm);
        Assert.Equal(0, repo.DeleteAttempts);
        Assert.True(vm.IsEditing);
        Assert.Equal("Alteração não salva", vm.Title);
    }

    [Fact]
    public async Task DeletingLastEditedItemResetsTheFormAndShowsEmptyState()
    {
        var repo = new MemoryRepository();
        repo.Alarms.Add(Sample());
        var vm = await Loaded(repo);
        vm.SelectedAlarm = vm.Alarms[0];
        vm.BeginEditSelected();
        vm.ConfirmDeleteAsync = _ => Task.FromResult(true);
        await vm.DeleteSelectedAsync();
        Assert.Empty(repo.Alarms);
        Assert.Empty(vm.Alarms);
        Assert.False(vm.IsEditing);
        Assert.Null(vm.SelectedAlarm);
        Assert.Equal("", vm.Title);
        Assert.Equal("Crie seu primeiro alarme.", vm.EmptyMessage);
        Assert.Equal("Alarme excluído.", vm.Message);
    }

    [Fact]
    public async Task DeletingAnotherSelectionPreservesCurrentEdit()
    {
        var repo = new MemoryRepository();
        var first = Sample(13);
        var second = Sample(14);
        repo.Alarms.AddRange([first, second]);
        var vm = await Loaded(repo);
        vm.SelectedAlarm = vm.Alarms[0];
        vm.BeginEditSelected();
        vm.Title = "Primeira alteração";
        vm.SelectedAlarm = vm.Alarms[1];
        Assert.Equal("Primeira alteração", vm.Title);
        vm.ConfirmDeleteAsync = _ => Task.FromResult(true);
        await vm.DeleteSelectedAsync();
        Assert.Equal(first, Assert.Single(repo.Alarms));
        Assert.True(vm.IsEditing);
        Assert.Equal("Primeira alteração", vm.Title);
        await vm.SaveAsync();
        Assert.Equal(first.Id, Assert.Single(repo.Alarms).Id);
        Assert.Equal("Primeira alteração", Assert.Single(repo.Alarms).Title);
    }

    [Fact]
    public async Task EditingAnotherSelectionReplacesTheForm()
    {
        var repo = new MemoryRepository();
        var first = Sample(13);
        var second = Sample(14) with { Title = "Mercado" };
        repo.Alarms.AddRange([first, second]);
        var vm = await Loaded(repo);
        vm.SelectedAlarm = vm.Alarms[0];
        vm.BeginEditSelected();
        vm.Title = "Não salvar";
        vm.SelectedAlarm = vm.Alarms[1];
        vm.BeginEditSelected();
        Assert.Equal("Mercado", vm.Title);
        Assert.Equal(TimeSpan.FromHours(14), vm.SelectedTime);
        Assert.Equal(0, repo.UpdateAttempts);
    }

    [Fact]
    public async Task FailedDeletionPreservesRowAndFormAndAllowsRetry()
    {
        var repo = new MemoryRepository { FailDelete = true };
        var original = Sample();
        repo.Alarms.Add(original);
        var vm = await Loaded(repo);
        vm.SelectedAlarm = vm.Alarms[0];
        vm.BeginEditSelected();
        vm.Title = "Alteração";
        vm.ConfirmDeleteAsync = _ => Task.FromResult(true);
        await vm.DeleteSelectedAsync();
        Assert.Equal(original, Assert.Single(vm.Alarms).Alarm);
        Assert.True(vm.IsEditing);
        Assert.Equal("Alteração", vm.Title);
        Assert.Contains("não foi possível excluir", vm.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(vm.DeleteCommand.CanExecute(null));
        repo.FailDelete = false;
        await vm.DeleteSelectedAsync();
        Assert.Empty(vm.Alarms);
    }

    [Fact]
    public async Task RepeatedDeleteWhileConfirmingOrWritingIsBlocked()
    {
        var repo = new MemoryRepository { DeleteGate = new TaskCompletionSource() };
        repo.Alarms.Add(Sample());
        var vm = await Loaded(repo);
        vm.SelectedAlarm = vm.Alarms[0];
        var confirmation = new TaskCompletionSource<bool>();
        var calls = 0;
        vm.ConfirmDeleteAsync = _ => { calls++; return confirmation.Task; };
        var deletion = vm.DeleteSelectedAsync();
        await vm.DeleteSelectedAsync();
        Assert.Equal(1, calls);
        Assert.Equal(0, repo.DeleteAttempts);
        Assert.False(vm.CanEdit);
        Assert.False(vm.SaveCommand.CanExecute(null));
        confirmation.SetResult(true);
        await repo.DeleteStarted.Task;
        await vm.DeleteSelectedAsync();
        Assert.Equal(1, calls);
        Assert.Single(vm.Alarms);
        repo.DeleteGate.SetResult();
        await deletion;
        Assert.Equal(1, repo.DeleteAttempts);
        Assert.Empty(vm.Alarms);
    }

    [Fact]
    public async Task ConfirmationFailurePreservesTheRecord()
    {
        var repo = new MemoryRepository();
        var original = Sample();
        repo.Alarms.Add(original);
        var vm = await Loaded(repo);
        vm.SelectedAlarm = vm.Alarms[0];
        vm.ConfirmDeleteAsync = _ => Task.FromException<bool>(new InvalidOperationException("Dialog failure"));
        await vm.DeleteSelectedAsync();
        Assert.Equal(original, Assert.Single(vm.Alarms).Alarm);
        Assert.Equal(0, repo.DeleteAttempts);
        Assert.True(vm.CanEdit);
        Assert.Contains("excluir", vm.Message);
    }

    [Fact]
    public async Task MissingEditedItemShowsMessageThenReloadResetsTheForm()
    {
        var repo = new MemoryRepository();
        repo.Alarms.Add(Sample());
        var vm = await Loaded(repo);
        vm.SelectedAlarm = vm.Alarms[0];
        vm.BeginEditSelected();
        vm.Title = "Alteração";
        repo.Alarms.Clear();
        await vm.SaveAsync();
        Assert.Contains("não existe mais", vm.Message);
        Assert.True(vm.IsEditing);
        Assert.Equal("Alteração", vm.Title);
        Assert.Empty(repo.Alarms);
        await vm.LoadAsync();
        Assert.False(vm.IsEditing);
        Assert.Empty(vm.Alarms);
        Assert.Contains("não existe mais", vm.Message);
    }

    [Fact]
    public async Task ReloadPreservesUncommittedEditForExistingItem()
    {
        var repo = new MemoryRepository();
        repo.Alarms.Add(Sample());
        var vm = await Loaded(repo);
        vm.SelectedAlarm = vm.Alarms[0];
        vm.BeginEditSelected();
        vm.Title = "Não perder";
        vm.SelectedTime = TimeSpan.FromHours(15);
        await vm.LoadAsync();
        Assert.True(vm.IsEditing);
        Assert.Equal("Não perder", vm.Title);
        Assert.Equal(TimeSpan.FromHours(15), vm.SelectedTime);
        Assert.NotNull(vm.SelectedAlarm);
        Assert.Equal(0, repo.UpdateAttempts);
    }
}
