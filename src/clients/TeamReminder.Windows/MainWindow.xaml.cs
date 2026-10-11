using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TeamReminder.Core;

namespace TeamReminder.Windows;

public sealed partial class MainWindow : Window
{
    private readonly AlarmsViewModel viewModel;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private bool initialized;

    public MainWindow(AlarmsViewModel viewModel)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        Root.DataContext = viewModel;
        viewModel.ConfirmDeleteAsync = ConfirmDeleteAsync;
        AppWindow.Resize(new global::Windows.Graphics.SizeInt32(1000, 700));
        timer.Tick += async (_, _) =>
        {
            viewModel.RefreshTime();
            await viewModel.ProcessDueAsync();
        };
        Closed += (_, _) => timer.Stop();
    }

    private async Task<bool> ConfirmDeleteAsync(Alarm alarm)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = Root.XamlRoot,
            Title = "Excluir alarme?",
            Content = $"{alarm.Title}\n{(alarm.DueAtUtc ?? alarm.ScheduledAtUtc).ToLocalTime():dd/MM/yyyy HH:mm}\n\nEsta exclusão não pode ser desfeita.",
            PrimaryButtonText = "Excluir",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }
    private async void Root_Loaded(object sender, RoutedEventArgs e)
    {
        if (initialized) return;
        initialized = true;
        await viewModel.LoadAsync();
        await viewModel.ProcessDueAsync();
        timer.Start();
    }
}
