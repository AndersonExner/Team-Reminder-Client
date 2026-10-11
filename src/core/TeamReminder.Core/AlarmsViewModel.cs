using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace TeamReminder.Core;

public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void Notify([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        Notify(name);
        return true;
    }
}

public sealed class AsyncCommand(Func<Task> execute, Func<bool> canExecute) : ICommand
{
    private bool running;
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => !running && canExecute();
    public void Refresh() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter)) return;
        running = true;
        Refresh();
        try { await execute(); }
        finally { running = false; Refresh(); }
    }
}

public sealed class AlarmRow(Alarm alarm, TimeProvider clock) : ObservableObject
{
    public Alarm Alarm { get; } = alarm;
    public string Title => Alarm.Title;
    public string Schedule => (Alarm.DueAtUtc ?? Alarm.ScheduledAtUtc).ToLocalTime().ToString("dd/MM/yyyy HH:mm");
    public string RecurrenceText => RecurrenceCalendar.Describe(Alarm.Recurrence, Alarm.Days);
    public string Status => Alarm.DueAtUtc is not { } due ? "Disparado"
        : due <= clock.GetUtcNow().UtcDateTime ? "Aguardando disparo" : "Agendado";
    public void Refresh() { Notify(nameof(Schedule)); Notify(nameof(Status)); }
}

public sealed class AlarmsViewModel : ObservableObject
{
    private readonly AlarmService service;
    private readonly TimeProvider clock;
    private readonly Func<TimeZoneInfo> zone;
    private string title = "", titleError = "", scheduleError = "", message = "";
    private DateTimeOffset? selectedDate;
    private TimeSpan? selectedTime;
    private AlarmRow? selectedAlarm;
    private Alarm? editingAlarm;
    private bool busy, loaded;
    private RecurrenceKind recurrence;
    private string recurrenceError = "", alertText = "", notificationStatus = "";
    private AlarmScheduler? scheduler;

    public AlarmsViewModel(AlarmService service, TimeProvider clock, Func<TimeZoneInfo>? zone = null)
    {
        this.service = service;
        this.clock = clock;
        this.zone = zone ?? (() => TimeZoneInfo.Local);
        selectedDate = Today();
        DismissAlertsCommand = new AsyncCommand(() => { AlertText = ""; return Task.CompletedTask; }, () => true);
        SaveCommand = new AsyncCommand(SaveAsync, () => CanEdit);
        ReloadCommand = new AsyncCommand(LoadAsync, () => !busy);
        EditCommand = new AsyncCommand(() => { BeginEditSelected(); return Task.CompletedTask; }, () => CanEdit && SelectedAlarm is not null);
        CancelEditCommand = new AsyncCommand(() => { CancelEdit(); return Task.CompletedTask; }, () => CanEdit && IsEditing);
        DeleteCommand = new AsyncCommand(DeleteSelectedAsync, () => CanEdit && SelectedAlarm is not null);
    }

    public ObservableCollection<AlarmRow> Alarms { get; } = [];
    public AsyncCommand SaveCommand { get; }
    public AsyncCommand ReloadCommand { get; }
    public AsyncCommand EditCommand { get; }
    public AsyncCommand CancelEditCommand { get; }
    public AsyncCommand DeleteCommand { get; }
    public AsyncCommand DismissAlertsCommand { get; }
    public RecurrenceKind Recurrence
    {
        get => recurrence;
        set { if (Set(ref recurrence, value)) { Notify(nameof(ShowWeekDays)); Notify(nameof(RecurrenceIndex)); } }
    }
    public int RecurrenceIndex
    {
        get => (int)Recurrence;
        set { if (value >= 0 && value <= 2) Recurrence = (RecurrenceKind)value; }
    }
    public bool ShowWeekDays => Recurrence == RecurrenceKind.Weekly;
    public string RecurrenceError { get => recurrenceError; private set => Set(ref recurrenceError, value); }
    public ObservableCollection<WeekDayOption> DayOptions { get; } =
    [
        new(WeekDays.Monday, "Segunda"), new(WeekDays.Tuesday, "Terça"),
        new(WeekDays.Wednesday, "Quarta"), new(WeekDays.Thursday, "Quinta"),
        new(WeekDays.Friday, "Sexta"), new(WeekDays.Saturday, "Sábado"), new(WeekDays.Sunday, "Domingo")
    ];
    public string AlertText
    {
        get => alertText;
        private set { if (Set(ref alertText, value)) Notify(nameof(HasAlerts)); }
    }
    public bool HasAlerts => !string.IsNullOrEmpty(AlertText);
    public string NotificationStatus { get => notificationStatus; private set => Set(ref notificationStatus, value); }
    public void SetNotificationStatus(string value) => NotificationStatus = value;
    public void ConfigureScheduler(AlarmScheduler value) => scheduler = value;
    public void ReportAlert(AlarmDelivery delivery)
    {
        var line = $"{delivery.Alarm.Title} — {delivery.OccurrenceUtc.ToLocalTime():dd/MM/yyyy HH:mm}";
        if (delivery.IsLate) line += " (aviso atrasado)";
        AlertText = string.Join(Environment.NewLine, new[] { line }
            .Concat(AlertText.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)).Take(10));
    }
    private DateTimeOffset Today()
    {
        var localZone = zone();
        return TimeZoneInfo.ConvertTime(clock.GetUtcNow(), localZone);
    }
    public Func<Alarm, Task<bool>> ConfirmDeleteAsync { get; set; } = _ => Task.FromResult(false);
    public string Title { get => title; set => Set(ref title, value); }
    public DateTimeOffset? SelectedDate { get => selectedDate; set => Set(ref selectedDate, value); }
    public TimeSpan? SelectedTime { get => selectedTime; set => Set(ref selectedTime, value); }
    public AlarmRow? SelectedAlarm
    {
        get => selectedAlarm;
        set { if (Set(ref selectedAlarm, value)) RefreshCommands(); }
    }
    public string TitleError { get => titleError; private set => Set(ref titleError, value); }
    public string ScheduleError { get => scheduleError; private set => Set(ref scheduleError, value); }
    public string Message { get => message; private set => Set(ref message, value); }
    public bool IsBusy => busy;
    public bool CanEdit => loaded && !busy;
    public bool IsEditing => editingAlarm is not null;
    public string FormTitle => IsEditing ? "Editar alarme" : "Novo alarme";
    public string SaveLabel => IsEditing ? "Salvar alterações" : "Salvar alarme";
    public string EmptyMessage => loaded && Alarms.Count == 0 ? "Crie seu primeiro alarme." : "";

    private void RefreshCommands()
    {
        SaveCommand.Refresh(); ReloadCommand.Refresh(); EditCommand.Refresh();
        CancelEditCommand.Refresh(); DeleteCommand.Refresh();
    }

    private void SetBusy(bool value)
    {
        busy = value;
        Notify(nameof(IsBusy)); Notify(nameof(CanEdit)); Notify(nameof(EmptyMessage));
        RefreshCommands();
    }

    private void NotifyMode()
    {
        Notify(nameof(IsEditing)); Notify(nameof(FormTitle)); Notify(nameof(SaveLabel));
        RefreshCommands();
    }

    public void BeginEditSelected()
    {
        if (!CanEdit || SelectedAlarm is null) return;
        editingAlarm = SelectedAlarm.Alarm;
        var localZone = zone();
        var local = TimeZoneInfo.ConvertTimeFromUtc((editingAlarm.DueAtUtc ?? editingAlarm.ScheduledAtUtc), localZone);
        Title = editingAlarm.Title;
        SelectedDate = new DateTimeOffset(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), localZone.GetUtcOffset(local));
        SelectedTime = local.TimeOfDay;
        Recurrence = editingAlarm.Recurrence;
        foreach (var day in DayOptions) day.IsSelected = editingAlarm.Days.HasFlag(day.Day);
        TitleError = ScheduleError = RecurrenceError = Message = "";
        NotifyMode();
    }

    public void CancelEdit()
    {
        if (!CanEdit || !IsEditing) return;
        ResetForm();
        Message = "Edição cancelada.";
    }

    private void ResetForm()
    {
        editingAlarm = null;
        Title = ""; SelectedDate = Today(); SelectedTime = null;
        Recurrence = RecurrenceKind.Once;
        foreach (var day in DayOptions) day.IsSelected = false;
        TitleError = ScheduleError = RecurrenceError = "";
        NotifyMode();
    }

    public async Task LoadAsync()
    {
        if (busy) return;
        loaded = false;
        SetBusy(true);
        Message = "Carregando alarmes...";
        try
        {
            var alarms = await service.ListAsync();
            ReplaceRows(alarms);
            loaded = true;
            Message = "";
            if (editingAlarm is not null)
            {
                var existing = alarms.FirstOrDefault(a => a.Id == editingAlarm.Id);
                if (existing is null)
                {
                    ResetForm();
                    Message = "O alarme em edição não existe mais. O formulário foi limpo.";
                }
                else editingAlarm = existing;
            }
        }
        catch (Exception)
        {
            Message = "Não foi possível carregar os alarmes. Seus dados foram preservados. Tente novamente.";
        }
        finally { SetBusy(false); }
    }

    public async Task SaveAsync()
    {
        if (!CanEdit) return;
        SetBusy(true);
        TitleError = ScheduleError = RecurrenceError = Message = "";
        try
        {
            var editing = editingAlarm;
            var days = DayOptions.Where(day => day.IsSelected).Aggregate(WeekDays.None, (value, day) => value | day.Day);
            var alarm = editing is null
                ? await service.CreateAsync(Title, SelectedDate?.Date, SelectedTime, zone(), Recurrence, days)
                : await service.UpdateAsync(editing, Title, SelectedDate?.Date, SelectedTime, zone(), Recurrence, days);
            ReplaceRows(Alarms.Select(row => row.Alarm).Where(a => a.Id != alarm.Id).Append(alarm).ToArray());
            ResetForm();
            Message = editing is null ? "Alarme salvo." : "Alarme atualizado.";
        }
        catch (AlarmValidationException ex)
        {
            if (ex.Field == "Title") TitleError = ex.Message;
            else if (ex.Field == "Recurrence") RecurrenceError = ex.Message;
            else ScheduleError = ex.Message;
        }
        catch (AlarmNotFoundException ex) { Message = ex.Message; }
        catch (Exception)
        {
            Message = "O alarme não foi salvo. Confira o acesso ao armazenamento e tente novamente.";
        }
        finally { SetBusy(false); }
    }

    public async Task DeleteSelectedAsync()
    {
        if (!CanEdit || SelectedAlarm is null) return;
        var target = SelectedAlarm.Alarm;
        SetBusy(true);
        Message = "";
        try
        {
            if (!await ConfirmDeleteAsync(target)) return;
            await service.DeleteAsync(target.Id);
            ReplaceRows(Alarms.Select(row => row.Alarm).Where(a => a.Id != target.Id).ToArray());
            if (editingAlarm?.Id == target.Id) ResetForm();
            Message = "Alarme excluído.";
        }
        catch (AlarmNotFoundException ex) { Message = ex.Message; }
        catch (Exception)
        {
            Message = "Não foi possível excluir o alarme. Tente novamente.";
        }
        finally { SetBusy(false); }
    }

    private void ReplaceRows(IEnumerable<Alarm> alarms)
    {
        var selectedId = SelectedAlarm?.Alarm.Id;
        var rows = alarms.OrderBy(a => a.DueAtUtc ?? a.ScheduledAtUtc).ThenBy(a => a.CreatedAtUtc)
            .ThenBy(a => a.Id).Select(a => new AlarmRow(a, clock)).ToArray();
        Alarms.Clear();
        foreach (var row in rows) Alarms.Add(row);
        SelectedAlarm = rows.FirstOrDefault(row => row.Alarm.Id == selectedId);
        Notify(nameof(EmptyMessage));
    }

    public async Task ProcessDueAsync()
    {
        if (!CanEdit || scheduler is null ||
            !Alarms.Any(row => row.Alarm.DueAtUtc is { } due && due <= clock.GetUtcNow().UtcDateTime)) return;
        SetBusy(true);
        try
        {
            var result = await scheduler.TickAsync();
            if (result is null) return;
            ReplaceRows(result.Alarms);
            if (editingAlarm is not null)
                editingAlarm = result.Alarms.FirstOrDefault(a => a.Id == editingAlarm.Id) ?? editingAlarm;
            if (result.Errors.Count > 0) Message = string.Join(Environment.NewLine, result.Errors);
        }
        catch (Exception)
        {
            Message = "Não foi possível verificar os alarmes. Uma nova tentativa será feita.";
        }
        finally { SetBusy(false); }
    }
    public void RefreshTime() { foreach (var row in Alarms) row.Refresh(); }
}

public sealed class WeekDayOption(WeekDays day, string label) : ObservableObject
{
    private bool selected;
    public WeekDays Day { get; } = day;
    public string Label { get; } = label;
    public bool IsSelected { get => selected; set => Set(ref selected, value); }
}
