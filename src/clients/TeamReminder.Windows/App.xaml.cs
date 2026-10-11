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

    public App() => InitializeComponent();

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // SID keeps the mutex isolated per Windows account, including multiple sessions.
        var sid = System.Security.Principal.WindowsIdentity.GetCurrent().User!.Value;
        instanceMutex = new Mutex(true, @"Global\TeamReminder.Client." + sid, out var first);
        if (!first) { instanceMutex.Dispose(); instanceMutex = null; Exit(); return; }

        var dataPath = LiteDbAlarmRepository.DefaultPath;
#if DEBUG
        dataPath = Environment.GetEnvironmentVariable("TEAMREMINDER_TEST_DATA_FILE") ?? dataPath;
#endif
        repository = new LiteDbAlarmRepository(dataPath);
        var clock = TimeProvider.System;
        var viewModel = new AlarmsViewModel(new AlarmService(repository, clock), clock);
        window = new MainWindow(viewModel);
        notifier = new WindowsAlarmNotifier(viewModel.ReportAlert, viewModel.SetNotificationStatus,
            () => window.DispatcherQueue.TryEnqueue(() => window.Activate()));
        viewModel.ConfigureScheduler(new AlarmScheduler(repository, notifier, clock));
        notifier.Initialize();
        window.Closed += (_, _) =>
        {
            notifier.Dispose();
            repository.Dispose();
            instanceMutex.ReleaseMutex();
            instanceMutex.Dispose();
        };
        window.Activate();
    }
}
