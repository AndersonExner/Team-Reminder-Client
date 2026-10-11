using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TeamReminder.Core;
using TeamReminder.Local;

namespace TeamReminder.Windows;

public sealed partial class AlarmEditorDialog : ContentDialog
{
    private readonly AlarmsViewModel viewModel;

    public AlarmEditorDialog(AlarmsViewModel viewModel, LocalSoundLibrary sounds, IntPtr windowHandle)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        DataContext = viewModel;
        SoundEditor.Configure(sounds, windowHandle, viewModel.Sound, allowDefault: true);
        SoundEditor.SelectionChanged += (_, _) => viewModel.Sound = SoundEditor.Selection;
    }

    private async void Save_Click(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        if (SoundEditor.IsBusy) { args.Cancel = true; return; }
        var deferral = args.GetDeferral();
        try { args.Cancel = !await viewModel.SaveAsync(); }
        finally { deferral.Complete(); }
    }

    private void Dialog_Closing(ContentDialog sender, ContentDialogClosingEventArgs args)
    {
        if (viewModel.IsBusy || SoundEditor.IsBusy) args.Cancel = true;
        else if (args.Result != ContentDialogResult.Primary) viewModel.DiscardForm();
    }

    public IReadOnlyList<Guid> TakeImportedIds() => SoundEditor.TakeImportedIds();
    public void StopPreview() => SoundEditor.StopPreview();
    public void DisposeSoundEditor() => SoundEditor.Dispose();

    private void Dialog_Opened(ContentDialog sender, ContentDialogOpenedEventArgs args) =>
        AlarmTitle.Focus(FocusState.Programmatic);
}
