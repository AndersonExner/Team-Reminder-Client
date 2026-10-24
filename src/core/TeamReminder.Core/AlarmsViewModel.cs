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
    public string Origin => Alarm.GroupId is null ? "Pessoal" : $"Grupo · {Alarm.GroupName}";
    public string Schedule => (Alarm.DueAtUtc ?? Alarm.ScheduledAtUtc).ToLocalTime().ToString("dd/MM/yyyy HH:mm");
    public bool HasLink => AlarmLink.TryGetUri(Alarm.Link?.Url, out _);
    public string ActionLabel => Alarm.Link?.Label ?? "Abrir link";
    public string ActionUrl => Alarm.Link?.Url ?? "";
    public string SoundText => Alarm.Sound.Label;
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
    private Guid draftId = Guid.NewGuid();
    public ObservableCollection<AlarmDestination> Destinations { get; } = [new(null,"Só para mim")];
    private AlarmDestination? destination;
    public AlarmDestination? Destination
    {
        get => destination ?? Destinations.FirstOrDefault();
        set { if(Set(ref destination,value)){Notify(nameof(DestinationHint));Notify(nameof(SaveLabel));} }
    }
    public string DestinationHint => Destination?.GroupId is null
        ? "Um alarme pessoal. Ao entrar em uma conta, ele pode ser sincronizado entre seus computadores."
        : $"Todos os participantes de {Destination.Label} receberão o alarme ao sincronizar. Publicar exige conexão.";
    public string GroupSelectionMessage { get; private set; } = "";
    public void ReportGroupSelection(string message){GroupSelectionMessage=message;Notify(nameof(GroupSelectionMessage));}
    public bool CanChooseDestination => CanEdit && !IsEditing;
    public void ConfigureGroupPublisher(IGroupAlarmPublisher publisher) => service.GroupPublisher = publisher;
    public void SetDestinations(IEnumerable<GroupSummary> groups)
    {
        Destinations.Clear();Destinations.Add(new(null,"Só para mim"));
        foreach(var group in groups.Where(x=>x.MyRole is GroupRole.Owner or GroupRole.Administrator))
            Destinations.Add(new(group.Id,group.Name));
        Destination=Destinations.First();
    }
    private bool busy, loaded, rowsStale;
    private RecurrenceKind recurrence;
    private AlarmSound sound = AlarmSound.Default;
    private string actionUrl = "", actionLabel = "", actionError = "";
    public string ActionUrl { get => actionUrl; set => Set(ref actionUrl, value); }
    public string ActionLabel { get => actionLabel; set => Set(ref actionLabel, value); }
    public string ActionError { get => actionError; private set => Set(ref actionError, value); }
    public Func<Uri, Task<bool>> LaunchLinkAsync { get; set; } = _ => Task.FromResult(false);
    public ObservableCollection<AlarmRow> ReceivedAlarms { get; } = [];
    private string soundError = "";
    public AlarmSound Sound { get => sound; set => Set(ref sound, value); }
    public string SoundError { get => soundError; private set => Set(ref soundError, value); }
    private string recurrenceError = "", alertText = "", notificationStatus = "";
    private AlarmScheduler? scheduler;
    private EarlyReminderScheduler? earlyScheduler;

    public AlarmsViewModel(AlarmService service, TimeProvider clock, Func<TimeZoneInfo>? zone = null)
    {
        this.service = service;
        this.clock = clock;
        this.zone = zone ?? (() => TimeZoneInfo.Local);
        var localNow = Today().AddMinutes(5);
        selectedDate = localNow;
        selectedTime = new TimeSpan(localNow.Hour, localNow.Minute, 0);
        DismissAlertsCommand = new AsyncCommand(() => { AlertText = ""; ReceivedAlarms.Clear(); return Task.CompletedTask; }, () => true);
        SaveCommand = new AsyncCommand(SaveAsync, () => CanEdit);
        ReloadCommand = new AsyncCommand(LoadAsync, () => !busy);
        EditCommand = new AsyncCommand(() => { BeginEditSelected(); return Task.CompletedTask; }, () => CanEditSelected);
        CancelEditCommand = new AsyncCommand(() => { CancelEdit(); return Task.CompletedTask; }, () => CanEdit && IsEditing);
        DeleteCommand = new AsyncCommand(DeleteSelectedAsync, () => CanEditSelected);
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
    public void ConfigureEarlyScheduler(EarlyReminderScheduler value) => earlyScheduler = value;
    public void ReportAlert(AlarmDelivery delivery)
    {
        ReceivedAlarms.Insert(0, new AlarmRow(delivery.Alarm, clock));
        while (ReceivedAlarms.Count > 10) ReceivedAlarms.RemoveAt(ReceivedAlarms.Count - 1);
        var line = $"{delivery.Alarm.Title} — {delivery.OccurrenceUtc.ToLocalTime():dd/MM/yyyy HH:mm}";
        if (delivery.IsLate) line += " (aviso atrasado)";
        AlertText = string.Join(Environment.NewLine, new[] { line }
            .Concat(AlertText.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)).Take(10));
    }
    public async Task OpenActionAsync(Alarm alarm)
    {
        if (!AlarmLink.TryGetUri(alarm.Link?.Url, out var uri))
        {
            Message = "Este alarme não possui um link HTTP/HTTPS válido."; return;
        }
        try
        {
            Message = await LaunchLinkAsync(uri!) ? "Link enviado ao navegador padrão."
                : "Não foi possível abrir o link. Confira o navegador padrão e tente novamente.";
        }
        catch (Exception) { Message = "Não foi possível abrir o link. Confira o navegador padrão e tente novamente."; }
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
    public bool CanEditSelected => CanEdit && SelectedAlarm is not null && (SelectedAlarm.Alarm.GroupId is null || SelectedAlarm.Alarm.CanManageGroup);
    public bool IsEditing => editingAlarm is not null;
    public string FormTitle => IsEditing ? "Editar alarme" : "Novo alarme";
    public string SaveLabel => IsEditing ? "Salvar alterações" : Destination?.GroupId is not null ? "Publicar no grupo" : "Salvar alarme";
    public string EmptyMessage => loaded && Alarms.Count == 0 ? "Crie seu primeiro alarme." : "";

    private void RefreshCommands()
    {
        Notify(nameof(CanEditSelected));
        SaveCommand.Refresh(); ReloadCommand.Refresh(); EditCommand.Refresh();
        CancelEditCommand.Refresh(); DeleteCommand.Refresh();
    }

    private void SetBusy(bool value)
    {
        busy = value;
        Notify(nameof(IsBusy)); Notify(nameof(CanChooseDestination)); Notify(nameof(CanEdit)); Notify(nameof(EmptyMessage));
        RefreshCommands();
    }

    private void NotifyMode()
    {
        Notify(nameof(CanChooseDestination)); Notify(nameof(IsEditing)); Notify(nameof(FormTitle)); Notify(nameof(SaveLabel));
        RefreshCommands();
    }

    public void BeginEditSelected()
    {
        if (!CanEdit || SelectedAlarm is null) return;
        if (!CanEditSelected) return;
        editingAlarm = SelectedAlarm.Alarm;
        Destination=editingAlarm.GroupId is null ? Destinations.FirstOrDefault(x=>x.GroupId is null)
            : Destinations.FirstOrDefault(x=>x.GroupId==editingAlarm.GroupId) ?? new(editingAlarm.GroupId,editingAlarm.GroupName??"Grupo");
        if(!Destinations.Any(x=>x.GroupId==Destination?.GroupId)&&Destination is not null)Destinations.Add(Destination);
        var localZone = zone();
        var local = TimeZoneInfo.ConvertTimeFromUtc((editingAlarm.DueAtUtc ?? editingAlarm.ScheduledAtUtc), localZone);
        Title = editingAlarm.Title;
        SelectedDate = new DateTimeOffset(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), localZone.GetUtcOffset(local));
        SelectedTime = local.TimeOfDay;
        ActionUrl = editingAlarm.Link?.Url ?? "";
        ActionLabel = editingAlarm.Link?.Label ?? "";
        Sound = editingAlarm.Sound;
        Recurrence = editingAlarm.Recurrence;
        foreach (var day in DayOptions) day.IsSelected = editingAlarm.Days.HasFlag(day.Day);
        TitleError = ScheduleError = RecurrenceError = SoundError = ActionError = Message = "";
        NotifyMode();
    }

    public void CancelEdit()
    {
        if (!CanEdit || !IsEditing) return;
        ResetForm();
        Message = "Edição cancelada.";
    }

    public void BeginCreate()
    {
        if (!CanEdit) return;
        ResetForm();
        Message = "";
    }

    public void DiscardForm()
    {
        if (!CanEdit) return;
        ResetForm();
    }

    private void ResetForm()
    {
        editingAlarm = null; draftId=Guid.NewGuid(); Destination=Destinations.FirstOrDefault();
        var localNow = Today().AddMinutes(5);
        Title = ""; SelectedDate = localNow; SelectedTime = new TimeSpan(localNow.Hour, localNow.Minute, 0);
        ActionUrl = ActionLabel = "";
        Sound = AlarmSound.Default;
        Recurrence = RecurrenceKind.Once;
        foreach (var day in DayOptions) day.IsSelected = false;
        TitleError = ScheduleError = RecurrenceError = SoundError = ActionError = "";
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

    public async Task<bool> SaveAsync()
    {
        if (!CanEdit) return false;
        SetBusy(true);
        TitleError = ScheduleError = RecurrenceError = SoundError = ActionError = Message = "";
        try
        {
            var editing = editingAlarm;
            var days = DayOptions.Where(day => day.IsSelected).Aggregate(WeekDays.None, (value, day) => value | day.Day);
            var alarm = editing is null
                ? await service.CreateAsync(Title, SelectedDate?.Date, SelectedTime, zone(), Recurrence, days, Sound, ActionUrl, ActionLabel, Destination?.GroupId, draftId)
                : await service.UpdateAsync(editing, Title, SelectedDate?.Date, SelectedTime, zone(), Recurrence, days, Sound, ActionUrl, ActionLabel);
            ReplaceRows(Alarms.Select(row => row.Alarm).Where(a => a.Id != alarm.Id).Append(alarm).ToArray());
            ResetForm();
            Message = editing is null ? "Alarme salvo." : "Alarme atualizado.";
            return true;
        }
        catch (AlarmValidationException ex)
        {
            if (ex.Field == "Title") TitleError = ex.Message;
            else if (ex.Field == "Action") ActionError = ex.Message;
            else if (ex.Field == "Sound") SoundError = ex.Message;
            else if (ex.Field == "Recurrence") RecurrenceError = ex.Message;
            else ScheduleError = ex.Message;
        }
        catch (GroupAlarmOperationException ex) { Message = ex.Message; }
        catch (AlarmNotFoundException ex) { Message = ex.Message; }
        catch (Exception)
        {
            Message = "O alarme não foi salvo. Confira o acesso ao armazenamento e tente novamente.";
        }
        finally { SetBusy(false); }
        return false;
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
            await service.DeleteAsync(target);
            ReplaceRows(Alarms.Select(row => row.Alarm).Where(a => a.Id != target.Id).ToArray());
            if (editingAlarm?.Id == target.Id) ResetForm();
            Message = "Alarme excluído.";
        }
        catch (GroupAlarmOperationException ex) { Message = ex.Message; }
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

    // Delivery never waits for the form: the schedulers read storage directly and guard their own concurrency,
    // and the repository rejects completing an occurrence whose alarm changed meanwhile.
    public async Task ProcessDueAsync()
    {
        if (scheduler is null) return;
        try
        {
            var result = await scheduler.TickAsync();
            if (result is not null)
            {
                if (result.Errors.Count > 0) Message = string.Join(Environment.NewLine, result.Errors);
                rowsStale = true;
            }
            // While a form operation runs, it owns the list; refresh it on a later tick.
            if (rowsStale && loaded && !busy)
            {
                rowsStale = false;
                var alarms = result?.Alarms ?? await service.ListAsync();
                ReplaceRows(alarms);
                if (editingAlarm is not null)
                    editingAlarm = alarms.FirstOrDefault(a => a.Id == editingAlarm.Id) ?? editingAlarm;
            }
        }
        catch (Exception)
        {
            Message = "Não foi possível verificar os alarmes. Uma nova tentativa será feita.";
        }
    }
    public async Task ProcessEarlyAsync()
    {
        if (earlyScheduler is null) return;
        try
        {
            var errors = await earlyScheduler.TickAsync();
            if (errors.Count > 0) Message = string.Join(Environment.NewLine, errors);
        }
        catch (Exception) { Message = "Não foi possível verificar os lembretes antecipados. Uma nova tentativa será feita."; }
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
