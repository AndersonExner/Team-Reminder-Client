using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using TeamReminder.Core;
using TeamReminder.Local;

namespace TeamReminder.Windows;

public sealed partial class MainWindow : Window
{
    private readonly AlarmsViewModel viewModel;
    private readonly LocalSoundLibrary sounds;
    private readonly IAlarmRepository repository;
    private AlarmEditorDialog? currentEditor;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private bool initialized;
    private bool themeReady;
    private bool startupReady;
    private bool exiting;
    private bool dialogOpen;
    private bool editorPreparing;
    private bool advanceReady;
    private bool updatesReady;
    private int ticks;
    private readonly AppUpdater updater = new();
    private bool autoUpdate = true;
    private bool updateReady;
    private bool maintainingUpdates;
    private readonly DateTime startedUtc = DateTime.UtcNow;
    private static readonly TimeSpan UpdateInterval = TimeSpan.FromHours(6);
    private static readonly string AutoUpdatePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TeamReminder", "Settings", "auto-update.txt");
    private int? advanceMinutes;
    private static readonly int?[] AdvanceOptions = [null, 5, 10, 15, 30, 60];
    private static readonly string AdvancePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TeamReminder", "Settings", "advance-minutes.txt");
    private TrayIcon? tray;
    private SnoozeScheduler? snooze;
    private Func<Task>? sendTestNotification;
    private static readonly string ThemePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TeamReminder", "Settings", "theme.txt");

    public MainWindow(AlarmsViewModel viewModel, LocalSoundLibrary sounds, IAlarmRepository repository)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        this.sounds = sounds;
        this.repository = repository;
        AccountPanel.Configure((AccountAlarmRepository)repository, async scopeChanged =>
        {
            if (scopeChanged)
            {
                UpdateAccountBadge();
                while (viewModel.IsBusy) await Task.Delay(20);
                currentEditor?.Hide();
                viewModel.DismissAlertsCommand.Execute(null);
                viewModel.DiscardForm();
            }
            if (dialogOpen || viewModel.IsBusy) return;
            await viewModel.LoadAsync();
        }, () => !dialogOpen && !viewModel.IsBusy);
        GroupsPanel.Configure(AccountPanel);
        AccountPanel.SessionChanged += (_, _) => UpdateAccountBadge();
        viewModel.ConfigureGroupPublisher(new GroupAlarmPublisher(AccountPanel,(AccountAlarmRepository)repository));
        viewModel.LaunchLinkAsync = async uri => await global::Windows.System.Launcher.LaunchUriAsync(uri);
        DefaultSoundEditor.Configure(sounds, WinRT.Interop.WindowNative.GetWindowHandle(this), sounds.DefaultSound, allowDefault: false);
        DefaultSoundEditor.SelectionChanged += DefaultSound_Changed;
        Root.DataContext = viewModel;
        var theme = ElementTheme.Light;
        try
        {
            if (File.Exists(ThemePath) && Enum.TryParse<ElementTheme>(File.ReadAllText(ThemePath), out var saved)
                && Enum.IsDefined(saved)) theme = saved;
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
#if DEBUG
        // UI checks can preview a theme without touching the saved preference.
        if (Environment.GetEnvironmentVariable("TEAMREMINDER_TEST_DATA_FILE") is { Length: > 0 }
            && Enum.TryParse<ElementTheme>(Environment.GetEnvironmentVariable("TEAMREMINDER_TEST_THEME"), out var preview)) theme = preview;
#endif
        Root.RequestedTheme = theme;
        ThemePicker.SelectedIndex = (int)theme;
        themeReady = true;
        try
        {
            if (File.Exists(AdvancePath) && int.TryParse(File.ReadAllText(AdvancePath), out var minutes)
                && AdvanceOptions.Contains(minutes)) advanceMinutes = minutes;
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        AdvancePicker.SelectedIndex = Array.IndexOf(AdvanceOptions, advanceMinutes);
        advanceReady = true;
        try { StartupToggle.IsOn = StartupRegistration.IsEnabled(); }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        { StartupStatus.Text = "Não foi possível consultar a inicialização com Windows."; }
        startupReady = true;
        try
        {
            if (File.Exists(AutoUpdatePath) && bool.TryParse(File.ReadAllText(AutoUpdatePath), out var savedAutoUpdate))
                autoUpdate = savedAutoUpdate;
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        AutoUpdateToggle.IsOn = autoUpdate;
        AutoUpdateToggle.IsEnabled = CheckUpdateButton.IsEnabled = AppUpdater.IsInstalled;
        UpdateVersion.Text = $"Team Reminder {AppUpdater.CurrentVersion}";
        if (!AppUpdater.IsInstalled) UpdateStatus.Text = "Atualização automática disponível apenas na versão instalada.";
        updatesReady = true;
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico"));
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        AppWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
        AppWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        TitleBarIcon.Source = new BitmapImage(new Uri(Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico")));
        Root.ActualThemeChanged += (_, _) => UpdateCaptionButtons();
        UpdateCaptionButtons();
        Nav.Loaded += (_, _) => { if (Nav.SettingsItem is NavigationViewItem item) item.Content = "Configurações"; };
        AppWindow.Closing += (_, e) =>
        {
            if (exiting || tray is null) return;
            e.Cancel = true;
            AppWindow.Hide();
        };
        viewModel.ConfirmDeleteAsync = ConfirmDeleteAsync;
        AppWindow.Resize(new global::Windows.Graphics.SizeInt32(1180, 840));
        timer.Tick += async (_, _) =>
        {
            // Windows time zone changes are not observed by TimeZoneInfo.Local until its cache is cleared.
            if (++ticks % 60 == 0) { TimeZoneInfo.ClearCachedData(); await MaintainUpdatesAsync(); }
            viewModel.RefreshTime();
            await viewModel.ProcessDueAsync();
            await viewModel.ProcessEarlyAsync();
            if (snooze is not null)
            {
                var errors = await snooze.TickAsync();
                if (errors.Count > 0) viewModel.ReportMessage(string.Join(Environment.NewLine, errors));
            }
            tray?.Update();
        };
        Closed += (_, _) => { AccountPanel.Shutdown(); DefaultSoundEditor.Dispose(); currentEditor?.DisposeSoundEditor(); timer.Stop(); tray?.Dispose(); tray = null; };
    }

    public Task InitializeAccountAsync() => AccountPanel.StartAsync();

    private async void Nav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        var page = args.IsSettingsSelected ? "settings" : (args.SelectedItem as NavigationViewItem)?.Tag as string ?? "home";
        HomePage.Visibility = page == "home" ? Visibility.Visible : Visibility.Collapsed;
        AlarmsPage.Visibility = page == "alarms" ? Visibility.Visible : Visibility.Collapsed;
        GroupsPage.Visibility = page == "groups" ? Visibility.Visible : Visibility.Collapsed;
        SettingsPage.Visibility = page == "settings" ? Visibility.Visible : Visibility.Collapsed;
        GroupsPanel.IsActive = page == "groups";
        if (GroupsPanel.IsActive) await GroupsPanel.RefreshAsync();
    }

    private void Navigate(string page) => Nav.SelectedItem = page switch
    {
        "alarms" => AlarmsItem, "groups" => GroupsItem, "settings" => Nav.SettingsItem, _ => HomeItem
    };

    private void ShowAlarms_Click(object sender, RoutedEventArgs e) => Navigate("alarms");

    private void UpdateAccountBadge()
    {
        var name = AccountPanel.CurrentUserName;
        AccountBadge.Text = name ?? "Local";
        viewModel.UserName = name ?? "";
    }

    // Caption buttons sit on Mica, so their glyphs follow the app theme instead of the system default.
    private void UpdateCaptionButtons()
    {
        var dark = Root.ActualTheme == ElementTheme.Dark;
        var foreground = dark ? Colors.White : Colors.Black;
        AppWindow.TitleBar.ButtonForegroundColor = foreground;
        AppWindow.TitleBar.ButtonHoverForegroundColor = foreground;
        AppWindow.TitleBar.ButtonHoverBackgroundColor = dark ? global::Windows.UI.Color.FromArgb(24, 255, 255, 255) : global::Windows.UI.Color.FromArgb(24, 0, 0, 0);
        AppWindow.TitleBar.ButtonInactiveForegroundColor = dark ? Colors.Gray : Colors.DimGray;
    }

    public void ConfigureEarlyReminders(IAlarmRepository repository, IEarlyReminderReceiptStore receipts, TimeProvider clock,
        Func<Alarm, int, bool> showNotification)
    {
        viewModel.ConfigureEarlyScheduler(new EarlyReminderScheduler(repository, receipts, alarm =>
        {
            // A Windows notification when available; the tray balloon remains the fallback.
            if (showNotification(alarm, advanceMinutes ?? 0)) return Task.CompletedTask;
            if (tray is null) throw new InvalidOperationException("Bandeja indisponível.");
            tray.ShowEarlyReminder(alarm);
            return Task.CompletedTask;
        }, () => advanceMinutes, clock));
    }

    public void ConfigureSnooze(SnoozeScheduler scheduler, Func<Task> testNotification)
    {
        snooze = scheduler;
        sendTestNotification = testNotification;
    }

    // Called from the notification's Adiar button and from the in-app list of received alarms.
    public async Task SnoozeAsync(Guid alarmId, DateTime occurrenceUtc, int minutes)
    {
        if (snooze is null) return;
        try
        {
            var until = await snooze.SnoozeAsync(alarmId, occurrenceUtc, TimeSpan.FromMinutes(minutes));
            var received = viewModel.ReceivedAlarms.FirstOrDefault(row => row.Alarm.Id == alarmId);
            if (received is not null) viewModel.DismissAlert(received);
            viewModel.ReportMessage($"Alarme adiado para {until.ToLocalTime():HH:mm}.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentOutOfRangeException)
        {
            ErrorLog.Write(ex);
            viewModel.ReportMessage("Não foi possível adiar o alarme. Tente novamente.");
        }
    }

    private static AlarmRow? RowOf(object sender) => (sender as FrameworkElement)?.Tag as AlarmRow;

    private void SnoozeReceived_Click(object sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is not { } row) return;
        var menu = new MenuFlyout();
        foreach (var minutes in SnoozeScheduler.Options)
        {
            var item = new MenuFlyoutItem { Text = $"{minutes} minutos" };
            item.Click += async (_, _) => await SnoozeAsync(row.Alarm.Id, row.OccurrenceUtc, minutes);
            menu.Items.Add(item);
        }
        menu.ShowAt((FrameworkElement)sender);
    }

    private void DismissReceived_Click(object sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is { } row) viewModel.DismissAlert(row);
    }

    private async void AlarmCard_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        if (RowOf(sender) is { CanManage: true } row) await EditRowAsync(row);
    }

    private async void EditRow_Click(object sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is { } row) await EditRowAsync(row);
    }

    private async Task EditRowAsync(AlarmRow row)
    {
        viewModel.SelectedAlarm = viewModel.Alarms.FirstOrDefault(candidate => candidate.Alarm.Id == row.Alarm.Id);
        if (viewModel.SelectedAlarm is not null) await ShowEditorAsync(edit: true);
    }

    private void AlarmMore_Click(object sender, RoutedEventArgs e)
    {
        if (RowOf(sender) is not { } row) return;
        var menu = new MenuFlyout();
        var edit = new MenuFlyoutItem { Text = "Editar", Icon = new FontIcon { Glyph = "\uE70F" }, IsEnabled = row.CanManage && viewModel.CanEdit };
        edit.Click += async (_, _) => await EditRowAsync(row);
        menu.Items.Add(edit);
        if (row.HasLink)
        {
            var open = new MenuFlyoutItem { Text = row.ActionLabel, Icon = new FontIcon { Glyph = "\uE71B" } };
            open.Click += async (_, _) => await viewModel.OpenActionAsync(row.Alarm);
            menu.Items.Add(open);
        }
        menu.Items.Add(new MenuFlyoutSeparator());
        var delete = new MenuFlyoutItem { Text = "Excluir", Icon = new FontIcon { Glyph = "\uE74D" }, IsEnabled = row.CanManage && viewModel.CanEdit };
        delete.Click += async (_, _) =>
        {
            viewModel.SelectedAlarm = viewModel.Alarms.FirstOrDefault(candidate => candidate.Alarm.Id == row.Alarm.Id);
            if (viewModel.SelectedAlarm is null) return;
            await viewModel.DeleteSelectedAsync();
            await CleanSoundsAsync();
        };
        menu.Items.Add(delete);
        menu.ShowAt((FrameworkElement)sender);
    }

    private async void TestNotification_Click(object sender, RoutedEventArgs e)
    {
        if (sendTestNotification is null) return;
        try
        {
            await sendTestNotification();
            TestStatus.Text = "Teste enviado. O botão Adiar repete apenas alarmes reais.";
        }
        catch (Exception ex)
        {
            ErrorLog.Write(ex);
            TestStatus.Text = "Não foi possível enviar o teste. Confira as notificações do Windows para o Team Reminder.";
        }
    }

    private void OpenLogs_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var folder = Path.GetDirectoryName(ErrorLog.FilePath)!;
            Directory.CreateDirectory(folder);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception) { ErrorLog.Write(ex); }
    }

    private void AdvancePicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!advanceReady || AdvancePicker.SelectedIndex < 0) return;
        advanceMinutes = AdvanceOptions[AdvancePicker.SelectedIndex];
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(AdvancePath)!);
            File.WriteAllText(AdvancePath, (advanceMinutes ?? 0).ToString());
            AdvanceStatus.Text = advanceMinutes is null ? "Lembretes antecipados desativados."
                : $"Avisar {advanceMinutes} minutos antes de todos os alarmes.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { AdvanceStatus.Text = "Preferência aplicada nesta sessão, mas não foi possível salvá-la."; }
    }

    public void StopSoundPreviews()
    {
        DefaultSoundEditor.StopPreview();
        currentEditor?.StopPreview();
    }

    private async void DefaultSound_Changed(object? sender, EventArgs e)
    {
        var selection = DefaultSoundEditor.Selection;
        if (!selection.IsValid)
        {
            DefaultSoundStatus.Text = "Selecione um arquivo para aplicar o novo padrão.";
            return;
        }
        DefaultSoundEditor.IsEnabled = false;
        try
        {
            await sounds.SaveDefaultAsync(selection);
            DefaultSoundStatus.Text = "Som padrão salvo neste computador.";
        }
        catch (Exception)
        {
            DefaultSoundEditor.SetSelection(sounds.DefaultSound);
            DefaultSoundStatus.Text = "Não foi possível salvar. O padrão anterior foi mantido.";
        }
        finally { DefaultSoundEditor.IsEnabled = true; }
        await CleanSoundsAsync(DefaultSoundEditor.TakeImportedIds());
    }

    private async void OpenLink_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: AlarmRow row }) await viewModel.OpenActionAsync(row.Alarm);
    }

    private async Task CleanSoundsAsync(IEnumerable<Guid>? releaseImports = null)
    {
        if (DefaultSoundEditor.IsBusy || dialogOpen) return;
        try
        {
            // A fresh successful read is required; never clean from an empty UI after a read failure.
            var alarms = await ((AccountAlarmRepository)repository).ListAllCachedAsync();
            if (DefaultSoundEditor.IsBusy || dialogOpen) return;
            await sounds.CleanupAsync(alarms.Select(a => a.Sound).Append(DefaultSoundEditor.Selection), releaseImports);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        catch (Exception) { /* Preserve all files when the database cannot be read. */ }
    }

    public bool EnableTray()
    {
        try
        {
            tray = new TrayIcon(viewModel,
                () => DispatcherQueue.TryEnqueue(Restore),
                () => DispatcherQueue.TryEnqueue(AddFromTray),
                () => DispatcherQueue.TryEnqueue(() => { Navigate("settings"); Restore(); }),
                () => DispatcherQueue.TryEnqueue(() => { exiting = true; Close(); }));
            return true;
        }
        catch (Exception)
        {
            viewModel.SetNotificationStatus("Bandeja indisponível. Mantenha a janela aberta para receber os alarmes.");
            return false;
        }
    }

    public void Restore()
    {
        AppWindow.Show();
        if (AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter) presenter.Restore();
        Activate();
    }

    private async void AddFromTray()
    {
        Restore();
        await ShowEditorAsync(edit: false);
    }

    private async void NewAlarm_Click(object sender, RoutedEventArgs e) => await ShowEditorAsync(edit: false);

    private async Task ShowEditorAsync(bool edit)
    {
        if (dialogOpen || editorPreparing || !viewModel.CanEdit) return;
        if (edit && !viewModel.CanEditSelected) return;
        editorPreparing=true;
        try
        {
            viewModel.ReportGroupSelection("");
            if(AccountPanel.CurrentAccountId is not null)
            {
                try { viewModel.SetDestinations(await AccountPanel.ExecuteAuthenticatedAsync((api,token)=>api.GroupsAsync(token))); }
                catch(Exception){viewModel.SetDestinations([]);viewModel.ReportGroupSelection("Não foi possível consultar seus grupos. Você ainda pode criar um alarme pessoal; reabra esta janela para tentar novamente.");}
            }
            else {viewModel.SetDestinations([]);viewModel.ReportGroupSelection("Entre na conta para publicar alarmes nos grupos em que você é administrador.");}
        }
        finally{editorPreparing=false;}
        if(!viewModel.CanEdit)return;
        dialogOpen = true;
        IReadOnlyList<Guid> importedIds = [];
        try
        {
            if (Root.XamlRoot is null)
            {
                var ready = new TaskCompletionSource();
                RoutedEventHandler loaded = (_, _) => ready.TrySetResult();
                Root.Loaded += loaded;
                try { Restore(); await ready.Task; }
                finally { Root.Loaded -= loaded; }
            }
            if (edit) viewModel.BeginEditSelected();
            else viewModel.BeginCreate();
            var dialog = new AlarmEditorDialog(viewModel, sounds, WinRT.Interop.WindowNative.GetWindowHandle(this))
            {
                XamlRoot = Root.XamlRoot,
                RequestedTheme = Root.RequestedTheme
            };
            currentEditor = dialog;
            try { await dialog.ShowAsync(); }
            finally { importedIds = dialog.TakeImportedIds(); dialog.DisposeSoundEditor(); currentEditor = null; }
        }
        finally
        {
            viewModel.DiscardForm();
            dialogOpen = false;
            await viewModel.LoadAsync();
            await CleanSoundsAsync(importedIds);
        }
    }
    // Checks a few minutes after start and every six hours; installs on its own only while the app sits
    // in the tray with nothing being edited and no alarm due soon. Otherwise the user chooses when.
    private async Task MaintainUpdatesAsync()
    {
        if (maintainingUpdates || exiting || !AppUpdater.IsInstalled || !autoUpdate) return;
        maintainingUpdates = true;
        try
        {
            var now = DateTime.UtcNow;
            if (!updateReady && now - startedUtc >= TimeSpan.FromMinutes(2)
                && (updater.LastCheckUtc is not { } last || now - last >= UpdateInterval))
                ShowUpdateStatus(await updater.CheckAsync());
            if (updateReady && !AppWindow.IsVisible && !dialogOpen && !editorPreparing && !viewModel.IsBusy
                && UpdatePolicy.IsQuietPeriod(await repository.ListAsync(), DateTime.UtcNow, TimeSpan.FromMinutes(10)))
                ApplyUpdate(showWindow: false);
        }
        catch (Exception ex) { ErrorLog.Write(ex); }
        finally { maintainingUpdates = false; }
    }

    private void ShowUpdateStatus(string text)
    {
        updateReady = updater.Ready is not null;
        UpdateStatus.Text = text;
        InstallUpdateButton.Visibility = updateReady ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ApplyUpdate(bool showWindow)
    {
        if (!updater.Launch(showWindow))
        {
            UpdateStatus.Text = "Não foi possível iniciar o instalador. Tente novamente mais tarde.";
            return;
        }
        exiting = true;
        Close();
    }

    private void AutoUpdateToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (!updatesReady) return;
        autoUpdate = AutoUpdateToggle.IsOn;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(AutoUpdatePath)!);
            File.WriteAllText(AutoUpdatePath, autoUpdate.ToString());
            UpdateStatus.Text = autoUpdate ? "Novas versões serão baixadas e instaladas quando o app estiver na bandeja."
                : "Atualização automática desativada. Use Verificar agora.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { UpdateStatus.Text = "Preferência aplicada nesta sessão, mas não foi possível salvá-la."; }
    }

    private async void CheckUpdate_Click(object sender, RoutedEventArgs e)
    {
        CheckUpdateButton.IsEnabled = false;
        UpdateStatus.Text = "Verificando atualizações...";
        try { ShowUpdateStatus(await updater.CheckAsync()); }
        finally { CheckUpdateButton.IsEnabled = true; }
    }

    private async void InstallUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (!UpdatePolicy.IsQuietPeriod(await repository.ListAsync(), DateTime.UtcNow, TimeSpan.FromMinutes(10)))
        {
            var dialog = new ContentDialog
            {
                XamlRoot = Root.XamlRoot, RequestedTheme = Root.RequestedTheme,
                Title = "Atualizar agora?",
                Content = "Há um alarme nos próximos 10 minutos. O app fecha por alguns segundos durante a instalação; alarmes nesse intervalo são avisados assim que ele reabrir.",
                PrimaryButtonText = "Atualizar", CloseButtonText = "Depois", DefaultButton = ContentDialogButton.Close
            };
            if (dialogOpen) return;
            dialogOpen = true;
            try { if (await dialog.ShowAsync() != ContentDialogResult.Primary) return; }
            finally { dialogOpen = false; }
        }
        ApplyUpdate(showWindow: true);
    }

    private void StartupToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (!startupReady) return;
        try
        {
            StartupRegistration.SetEnabled(StartupToggle.IsOn);
            StartupStatus.Text = StartupToggle.IsOn
                ? "O app iniciará na bandeja ao entrar no Windows."
                : "Inicialização com Windows desativada.";
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            startupReady = false;
            StartupToggle.IsOn = !StartupToggle.IsOn;
            startupReady = true;
            StartupStatus.Text = "Não foi possível alterar a inicialização. Tente novamente.";
        }
    }



    private void ThemePicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!themeReady || ThemePicker.SelectedIndex < 0) return;
        var theme = (ElementTheme)ThemePicker.SelectedIndex;
        Root.RequestedTheme = theme;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ThemePath)!);
            File.WriteAllText(ThemePath, theme.ToString());
            ThemeStatus.Text = "Preferência salva neste computador.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ThemeStatus.Text = "Tema aplicado, mas não foi possível salvar a preferência. Tente novamente.";
        }
    }

    private async Task<bool> ConfirmDeleteAsync(Alarm alarm)
    {
        if (dialogOpen) return false;
        dialogOpen = true;
        var dialog = new ContentDialog
        {
            XamlRoot = Root.XamlRoot,
            RequestedTheme = Root.RequestedTheme,
            Title = "Excluir alarme?",
            Content = (alarm.GroupId is null ? "" : $"Este alarme será excluído para todos os participantes de {alarm.GroupName}.\n\n") + $"{alarm.Title}\n{(alarm.DueAtUtc ?? alarm.ScheduledAtUtc).ToLocalTime():dd/MM/yyyy HH:mm}\n\nEsta exclusão não pode ser desfeita.",
            PrimaryButtonText = "Excluir",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close
        };
        try { return await dialog.ShowAsync() == ContentDialogResult.Primary; }
        finally { dialogOpen = false; }
    }
    private async void Root_Loaded(object sender, RoutedEventArgs e) => await StartAsync();

    public async Task StartAsync()
    {
        if (initialized) return;
        initialized = true;
        await viewModel.LoadAsync();
        await CleanSoundsAsync();
        await viewModel.ProcessDueAsync();
        timer.Start();
#if DEBUG
        // Lets UI checks open a given page (home, alarms, groups, settings) against isolated test data.
        if (Environment.GetEnvironmentVariable("TEAMREMINDER_TEST_DATA_FILE") is { Length: > 0 }
            && Environment.GetEnvironmentVariable("TEAMREMINDER_TEST_PAGE") is { Length: > 0 } page) Navigate(page);
#endif
    }
}
