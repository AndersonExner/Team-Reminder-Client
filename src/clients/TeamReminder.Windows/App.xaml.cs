using Microsoft.UI.Xaml;
using TeamReminder.Core;
using TeamReminder.Local;

namespace TeamReminder.Windows;

public partial class App : Application
{
    private Mutex? instanceMutex;
    private AccountAlarmRepository? repository;
    private MainWindow? window;
    private WindowsAlarmNotifier? notifier;
    private AlarmSoundPlayer? soundPlayer;
    private readonly CancellationTokenSource activationCancellation = new();

    public App()
    {
        InitializeComponent();
        // Keep the process (and its alarms) alive after an unexpected UI error, and leave a trace to diagnose it.
        UnhandledException += (_, e) => { ErrorLog.Write(e.Exception); e.Handled = true; };
        TaskScheduler.UnobservedTaskException += (_, e) => { ErrorLog.Write(e.Exception); e.SetObserved(); };
        AppDomain.CurrentDomain.UnhandledException += (_, e) => ErrorLog.Write(e.ExceptionObject as Exception);
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        try { await LaunchAsync(); }
        catch (Exception ex)
        {
            ErrorLog.Write(ex);
            // Without a window there is nothing left running; exit instead of lingering invisibly in the tray.
            if (window is null) Exit();
        }
    }

    private async Task LaunchAsync()
    {
        // SID keeps the mutex isolated per Windows account, including multiple sessions.
        var sid = System.Security.Principal.WindowsIdentity.GetCurrent().User!.Value;
#if DEBUG
        var isolatedData = Environment.GetEnvironmentVariable("TEAMREMINDER_TEST_DATA_FILE");
        if (!string.IsNullOrEmpty(isolatedData))
        {
            var identity = System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(Path.GetFullPath(isolatedData).ToUpperInvariant()));
            sid += ".Test." + Convert.ToHexString(identity)[..16];
        }
#endif
        instanceMutex = new Mutex(true, @"Global\TeamReminder.Client." + sid, out var first);
        if (!first)
        {
            instanceMutex.Dispose(); instanceMutex = null;
            if (!Environment.GetCommandLineArgs().Contains("--tray", StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    using var client = new System.IO.Pipes.NamedPipeClientStream(".",
                        "TeamReminder.Activation." + sid, System.IO.Pipes.PipeDirection.Out,
                        System.IO.Pipes.PipeOptions.Asynchronous | System.IO.Pipes.PipeOptions.CurrentUserOnly);
                    await client.ConnectAsync(3000);
                    await client.WriteAsync(new byte[] { 1 });
                }
                catch (Exception ex) when (ex is IOException or TimeoutException) { }
            }
            Exit(); return;
        }

        var dataPath = LiteDbAlarmRepository.DefaultPath;
#if DEBUG
        dataPath = Environment.GetEnvironmentVariable("TEAMREMINDER_TEST_DATA_FILE") ?? dataPath;
#endif
        repository = new AccountAlarmRepository(dataPath);
        var clock = TimeProvider.System;
        var viewModel = new AlarmsViewModel(new AlarmService(repository, clock), clock);
        var localRoot = Path.GetDirectoryName(Path.GetDirectoryName(dataPath)!)!;
#if DEBUG
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TEAMREMINDER_TEST_DATA_FILE")))
            localRoot = Path.GetDirectoryName(Path.GetFullPath(dataPath))!;
#endif
        var sounds = new LocalSoundLibrary(localRoot);
        await sounds.LoadDefaultAsync();
        window = new MainWindow(viewModel, sounds, repository);
        soundPlayer = new AlarmSoundPlayer(sounds, text => window.DispatcherQueue.TryEnqueue(() => viewModel.SetNotificationStatus(text)));
        soundPlayer.AlarmStarting += window.StopSoundPreviews;
        notifier = new WindowsAlarmNotifier(viewModel.ReportAlert, viewModel.SetNotificationStatus,
            () => window.DispatcherQueue.TryEnqueue(window.Restore), soundPlayer);
        viewModel.ConfigureScheduler(new AlarmScheduler(repository, notifier, clock));
        notifier.Initialize();
        window.ConfigureEarlyReminders(repository, repository, clock, notifier.ShowEarlyReminder);
        var snooze = new SnoozeScheduler(repository, new SnoozeFileStore(), notifier, clock);
        notifier.SnoozeRequested += (id, occurrence, minutes) =>
            window.DispatcherQueue.TryEnqueue(async () => await window.SnoozeAsync(id, occurrence, minutes));
        window.ConfigureSnooze(snooze, () =>
        {
            var now = clock.GetUtcNow().UtcDateTime;
            var sample = new Alarm(Guid.NewGuid(), "Alarme de teste do Team Reminder", now, now, TimeZoneInfo.Local.Id);
            return notifier.NotifyAsync(new AlarmDelivery(sample, now, false));
        });
        await window.InitializeAccountAsync();
        window.Closed += (_, _) =>
        {
            activationCancellation.Cancel();
            notifier.Dispose();
            soundPlayer.Dispose();
            repository.Dispose();
            instanceMutex.ReleaseMutex();
            instanceMutex.Dispose();
        };
        var trayAvailable = window.EnableTray();
        var startInTray = Environment.GetCommandLineArgs().Contains("--tray", StringComparer.OrdinalIgnoreCase);
        if (!startInTray || !trayAvailable) window.Activate();
        await window.StartAsync();
        _ = ListenForActivationAsync("TeamReminder.Activation." + sid, activationCancellation.Token);
        try
        {
            var activation = Microsoft.Windows.AppLifecycle.AppInstance.GetCurrent().GetActivatedEventArgs();
            if (activation.Kind == Microsoft.Windows.AppLifecycle.ExtendedActivationKind.AppNotification
                && activation.Data is Microsoft.Windows.AppNotifications.AppNotificationActivatedEventArgs toast)
                notifier.Handle(toast);
        }
        catch (Exception ex) { ErrorLog.Write(ex); }
#if DEBUG
        var editorReport=Environment.GetEnvironmentVariable("TEAMREMINDER_TEST_EDITOR_REPORT");
        if(!string.IsNullOrEmpty(editorReport)&&!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TEAMREMINDER_TEST_DATA_FILE")))
        {
            await EditorSmokeProbe.RunAsync(window,viewModel,sounds,editorReport);
            Exit();return;
        }
        var toastReport = Environment.GetEnvironmentVariable("TEAMREMINDER_TEST_TOAST_REPORT");
        if (!string.IsNullOrEmpty(toastReport) && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TEAMREMINDER_TEST_DATA_FILE")))
        {
            await ToastSmokeProbe.RunAsync(toastReport);
            Exit(); return;
        }
        var audioReport = Environment.GetEnvironmentVariable("TEAMREMINDER_TEST_AUDIO_REPORT");
        if (!string.IsNullOrEmpty(audioReport) && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TEAMREMINDER_TEST_DATA_FILE")))
            await AudioSmokeProbe.RunAsync(sounds, audioReport, WinRT.Interop.WindowNative.GetWindowHandle(window), viewModel);
        var windowLog = Environment.GetEnvironmentVariable("TEAMREMINDER_TEST_WINDOW_LOG");
        if (!string.IsNullOrEmpty(windowLog))
        {
            try
            {
                File.WriteAllText(windowLog, System.Text.Json.JsonSerializer.Serialize(new
                {
                    Handle = WinRT.Interop.WindowNative.GetWindowHandle(window).ToInt64(),
                    TrayAvailable = trayAvailable, window.Title, viewModel.NotificationStatus
                }));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
#endif
    }
    private async Task ListenForActivationAsync(string pipeName, CancellationToken cancellation)
    {
        try
        {
            while (!cancellation.IsCancellationRequested)
            {
                using var server = new System.IO.Pipes.NamedPipeServerStream(pipeName,
                    System.IO.Pipes.PipeDirection.In, 1, System.IO.Pipes.PipeTransmissionMode.Byte,
                    System.IO.Pipes.PipeOptions.Asynchronous | System.IO.Pipes.PipeOptions.CurrentUserOnly);
                await server.WaitForConnectionAsync(cancellation);
                var signal = new byte[1];
                if (await server.ReadAsync(signal, cancellation) > 0)
                    window!.DispatcherQueue.TryEnqueue(window.Restore);
            }
        }
        catch (OperationCanceledException) { }
        catch (IOException) { /* Activation failure does not interrupt alarms. */ }
    }
}
