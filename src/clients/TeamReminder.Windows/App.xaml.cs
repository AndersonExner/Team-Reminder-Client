using Microsoft.UI.Xaml;
using TeamReminder.Core;
using TeamReminder.Local;

namespace TeamReminder.Windows;

public partial class App : Application
{
    private Mutex? instanceMutex;
    private LiteDbAlarmRepository? repository;
    private MainWindow? window;
    private WindowsAlarmNotifier? notifier;
    private readonly CancellationTokenSource activationCancellation = new();

    public App() => InitializeComponent();

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        // SID keeps the mutex isolated per Windows account, including multiple sessions.
        var sid = System.Security.Principal.WindowsIdentity.GetCurrent().User!.Value;
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
        repository = new LiteDbAlarmRepository(dataPath);
        var clock = TimeProvider.System;
        var viewModel = new AlarmsViewModel(new AlarmService(repository, clock), clock);
        window = new MainWindow(viewModel);
        notifier = new WindowsAlarmNotifier(viewModel.ReportAlert, viewModel.SetNotificationStatus,
            () => window.DispatcherQueue.TryEnqueue(window.Restore));
        viewModel.ConfigureScheduler(new AlarmScheduler(repository, notifier, clock));
        notifier.Initialize();
        window.ConfigureEarlyReminders(repository, repository, clock);
        window.Closed += (_, _) =>
        {
            activationCancellation.Cancel();
            notifier.Dispose();
            repository.Dispose();
            instanceMutex.ReleaseMutex();
            instanceMutex.Dispose();
        };
        var trayAvailable = window.EnableTray();
        var startInTray = Environment.GetCommandLineArgs().Contains("--tray", StringComparer.OrdinalIgnoreCase);
        if (!startInTray || !trayAvailable) window.Activate();
        await window.StartAsync();
        _ = ListenForActivationAsync("TeamReminder.Activation." + sid, activationCancellation.Token);
#if DEBUG
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
