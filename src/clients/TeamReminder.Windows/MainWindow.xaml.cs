using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TeamReminder.Core;

namespace TeamReminder.Windows;

public sealed partial class MainWindow : Window
{
    private readonly AlarmsViewModel viewModel;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private bool initialized;
    private bool themeReady;
    private bool startupReady;
    private bool exiting;
    private bool dialogOpen;
    private bool advanceReady;
    private int? advanceMinutes;
    private static readonly int?[] AdvanceOptions = [null, 5, 10, 15, 30, 60];
    private static readonly string AdvancePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TeamReminder", "Settings", "advance-minutes.txt");
    private TrayIcon? tray;
    private static readonly string ThemePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TeamReminder", "Settings", "theme.txt");

    public MainWindow(AlarmsViewModel viewModel)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        Root.DataContext = viewModel;
        var theme = ElementTheme.Light;
        try
        {
            if (File.Exists(ThemePath) && Enum.TryParse<ElementTheme>(File.ReadAllText(ThemePath), out var saved)
                && Enum.IsDefined(saved)) theme = saved;
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
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
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico"));
        AppWindow.Closing += (_, e) =>
        {
            if (exiting || tray is null) return;
            e.Cancel = true;
            AppWindow.Hide();
        };
        viewModel.ConfirmDeleteAsync = ConfirmDeleteAsync;
        AppWindow.Resize(new global::Windows.Graphics.SizeInt32(1160, 820));
        timer.Tick += async (_, _) =>
        {
            viewModel.RefreshTime();
            await viewModel.ProcessDueAsync();
            await viewModel.ProcessEarlyAsync();
            tray?.Update();
        };
        Closed += (_, _) => { timer.Stop(); tray?.Dispose(); tray = null; };
    }

    public void ConfigureEarlyReminders(IAlarmRepository repository, IEarlyReminderReceiptStore receipts, TimeProvider clock)
    {
        viewModel.ConfigureEarlyScheduler(new EarlyReminderScheduler(repository, receipts, alarm =>
        {
            if (tray is null) throw new InvalidOperationException("Bandeja indisponível.");
            tray.ShowEarlyReminder(alarm);
            return Task.CompletedTask;
        }, () => advanceMinutes, clock));
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

    public bool EnableTray()
    {
        try
        {
            tray = new TrayIcon(viewModel,
                () => DispatcherQueue.TryEnqueue(Restore),
                () => DispatcherQueue.TryEnqueue(AddFromTray),
                () => DispatcherQueue.TryEnqueue(() => { MainTabs.SelectedIndex = 1; Restore(); }),
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
        MainTabs.SelectedIndex = 0;
        Restore();
        await ShowEditorAsync(edit: false);
    }

    private async void NewAlarm_Click(object sender, RoutedEventArgs e) => await ShowEditorAsync(edit: false);
    private async void EditAlarm_Click(object sender, RoutedEventArgs e) => await ShowEditorAsync(edit: true);

    private async Task ShowEditorAsync(bool edit)
    {
        if (dialogOpen || !viewModel.CanEdit) return;
        if (edit && !viewModel.CanEditSelected) return;
        dialogOpen = true;
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
            var dialog = new AlarmEditorDialog(viewModel)
            {
                XamlRoot = Root.XamlRoot,
                RequestedTheme = Root.RequestedTheme
            };
            await dialog.ShowAsync();
        }
        finally
        {
            viewModel.DiscardForm();
            dialogOpen = false;
        }
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
            Content = $"{alarm.Title}\n{(alarm.DueAtUtc ?? alarm.ScheduledAtUtc).ToLocalTime():dd/MM/yyyy HH:mm}\n\nEsta exclusão não pode ser desfeita.",
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
        await viewModel.ProcessDueAsync();
        timer.Start();
    }
}
