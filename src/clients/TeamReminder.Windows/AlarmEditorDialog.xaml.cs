using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TeamReminder.Core;

namespace TeamReminder.Windows;

public sealed partial class AlarmEditorDialog : ContentDialog
{
    private readonly AlarmsViewModel viewModel;

    public AlarmEditorDialog(AlarmsViewModel viewModel)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        DataContext = viewModel;
    }

    private async void Save_Click(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var deferral = args.GetDeferral();
        try { args.Cancel = !await viewModel.SaveAsync(); }
        finally { deferral.Complete(); }
    }

    private void Dialog_Closing(ContentDialog sender, ContentDialogClosingEventArgs args)
    {
        if (viewModel.IsBusy) args.Cancel = true;
        else if (args.Result != ContentDialogResult.Primary) viewModel.DiscardForm();
    }

    private void Dialog_Opened(ContentDialog sender, ContentDialogOpenedEventArgs args) =>
        AlarmTitle.Focus(FocusState.Programmatic);
}
