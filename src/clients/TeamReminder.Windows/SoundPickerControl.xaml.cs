using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TeamReminder.Core;
using TeamReminder.Local;
using Windows.Storage.Pickers;

namespace TeamReminder.Windows;

public sealed partial class SoundPickerControl : UserControl, IDisposable
{
    private LocalSoundLibrary? library;
    private AlarmSoundPlayer? preview;
    private IntPtr windowHandle;
    private int offset;
    private bool ready;
    private AlarmSound? custom;
    private readonly List<Guid> importedIds = [];
    public IReadOnlyList<Guid> TakeImportedIds() { var ids = importedIds.ToArray(); importedIds.Clear(); return ids; }
    public AlarmSound Selection { get; private set; } = AlarmSound.Default;
    public bool IsBusy { get; private set; }
    public event EventHandler? SelectionChanged;

    public SoundPickerControl() => InitializeComponent();

    public void Configure(LocalSoundLibrary sounds, IntPtr hwnd, AlarmSound selection, bool allowDefault)
    {
        library = sounds; windowHandle = hwnd;
        preview = new AlarmSoundPlayer(sounds, message => DispatcherQueue.TryEnqueue(() => SoundStatus.Text = message));
        if (!allowDefault) { offset = 1; KindPicker.Items.RemoveAt(0); }
        SetSelection(selection);
    }

    public void SetSelection(AlarmSound selection)
    {
        ready = false;
        Selection = selection;
        custom = selection.Kind == AlarmSoundKind.Custom ? selection : null;
        KindPicker.SelectedIndex = (int)selection.Kind - offset;
        ready = true;
        UpdateControls();
    }

    private void UpdateControls()
    {
        FilePanel.Visibility = Selection.Kind == AlarmSoundKind.Custom ? Visibility.Visible : Visibility.Collapsed;
        FileNameLabel.Text = Selection.FileName ?? "Nenhum arquivo selecionado.";
        KindPicker.IsEnabled = ChooseButton.IsEnabled = PreviewButton.IsEnabled = !IsBusy;
        PreviewButton.IsEnabled = !IsBusy && Selection.IsValid;
    }

    private void Kind_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!ready || KindPicker.SelectedIndex < 0) return;
        StopPreview();
        var kind = (AlarmSoundKind)(KindPicker.SelectedIndex + offset);
        if (kind != AlarmSoundKind.Custom) custom = null;
        Selection = kind == AlarmSoundKind.Custom ? custom ?? new AlarmSound(kind) : new AlarmSound(kind);
        SoundStatus.Text = "";
        UpdateControls();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private async void Choose_Click(object sender, RoutedEventArgs e)
    {
        if (IsBusy || library is null) return;
        StopPreview(); IsBusy = true; UpdateControls();
        var imported = false;
        try
        {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".mp3"); picker.FileTypeFilter.Add(".wav");
            WinRT.Interop.InitializeWithWindow.Initialize(picker, windowHandle);
            var file = await picker.PickSingleFileAsync();
            if (file is null) return;
            SoundStatus.Text = "Copiando e verificando áudio…";
            custom = await library.ImportAsync(file.Path, AlarmSoundPlayer.ValidateFileAsync);
            Selection = custom;
            importedIds.Add(custom.FileId!.Value);
            imported = true;
            SoundStatus.Text = "Cópia do áudio pronta.";
        }
        catch (Exception ex)
        {
            SoundStatus.Text = ex is InvalidDataException ? ex.Message
                : "Não foi possível importar o áudio. Confira o arquivo e tente novamente.";
        }
        finally { IsBusy = false; UpdateControls(); }
        if (imported) SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private async void Preview_Click(object sender, RoutedEventArgs e)
    {
        if (IsBusy || preview is null || !Selection.IsValid) return;
        IsBusy = true; SoundStatus.Text = ""; UpdateControls();
        try
        {
            if (Selection.Kind == AlarmSoundKind.Silent ||
                (Selection.Kind == AlarmSoundKind.Default && library!.DefaultSound.Kind == AlarmSoundKind.Silent))
                SoundStatus.Text = "Este alarme está sem som.";
            await preview.PlayAsync(Selection);
        }
        catch (Exception) { SoundStatus.Text = "Não foi possível ouvir o áudio."; }
        finally { IsBusy = false; UpdateControls(); }
    }

    private void Stop_Click(object sender, RoutedEventArgs e) => StopPreview();
    public void StopPreview() => preview?.Stop();
    public void Dispose() { ready = false; preview?.Dispose(); preview = null; }
}
