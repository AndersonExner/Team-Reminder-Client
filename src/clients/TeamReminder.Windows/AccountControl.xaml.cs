using System.Net.Http;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TeamReminder.Core;
using TeamReminder.Local;

namespace TeamReminder.Windows;

public sealed partial class AccountControl : UserControl
{
    private AuthClient? api;
    private AccountSessionStore? store;
    private AccountSession? session;
    private AccountAlarmRepository? repository;
    private Func<bool, Task>? changed;
    private Func<bool> canSynchronize = () => true;
    private bool shutdown;
    private string? server;
    private bool initialized;
    private bool busy;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMinutes(1) };
    public event EventHandler? SessionChanged;
    public bool HasSession => session is not null;
    public Guid? CurrentAccountId => session?.User.Id;

    // Group operations share the authentication path with alarm sync, preventing concurrent token rotation.
    public async Task<T> ExecuteAuthenticatedAsync<T>(Func<AuthClient, string, Task<T>> operation)
    {
        while (busy)
        {
            if (shutdown) throw new OperationCanceledException();
            await Task.Delay(100);
        }
        if (session is null || api is null) throw new AccountApiException("Entre na sua conta em Configurações para usar grupos.");
        SetBusy(true);
        try
        {
            await EnsureAccessAsync();
            try { return await operation(api, session.AccessToken); }
            catch (AccountApiException ex) when (ex.Unauthorized)
            { await RefreshAsync(); return await operation(api, session!.AccessToken); }
        }
        catch (AccountApiException ex) when (ex.Unauthorized)
        { await HandleErrorAsync(ex); throw; }
        finally { SetBusy(false); }
    }

    public AccountControl()
    {
        InitializeComponent();
        timer.Tick += async (_, _) => await SynchronizeAsync();
    }
    public void Configure(AccountAlarmRepository alarms, Func<bool, Task> onChanged, Func<bool> canRun)
    { repository = alarms; changed = onChanged; canSynchronize = canRun; }
    public void Shutdown() { shutdown = true; timer.Stop(); }

    private void Register_Toggled(object sender, RoutedEventArgs e)
    {
        if (NameInput is null) return;
        var registering = RegisterToggle.IsOn;
        NameInput.Visibility = ConfirmInput.Visibility = PasswordHint.Visibility = registering ? Visibility.Visible : Visibility.Collapsed;
        SubmitButton.Content = registering ? "Criar conta" : "Entrar";
        PasswordInput.Password = ConfirmInput.Password = "";
        AccountStatus.Text = "";
    }

    private async void Account_Loaded(object sender, RoutedEventArgs e) => await StartAsync();

    // Read the saved session/cache before starting the alarm scheduler; network checks run independently.
    public async Task StartAsync()
    {
        if (initialized || repository is null) return;
        initialized = true;
        try
        {
            var address = AuthClient.ServerAddress(Environment.GetEnvironmentVariable("TEAMREMINDER_API_URL"));
            server = address.AbsoluteUri;
            api = new AuthClient(new HttpClient { BaseAddress = address, Timeout = TimeSpan.FromSeconds(60) });
            store = new AccountSessionStore(server);
            session = await store.LoadAsync();
            if (session is not null && session.RefreshExpiresAt <= DateTimeOffset.UtcNow)
            { store.Clear(); session = null; }
            await SelectScopeAsync();
            timer.Start();
            if (session is not null) _ = RestoreAsync();
        }
        catch (Exception ex) { ReportError(ex); }
    }

    private void SetBusy(bool value)
    {
        busy = value;
        SubmitButton.IsEnabled = RegisterToggle.IsEnabled = NameInput.IsEnabled = EmailInput.IsEnabled =
            PasswordInput.IsEnabled = ConfirmInput.IsEnabled = LogoutButton.IsEnabled = RetryButton.IsEnabled =
            SyncButton.IsEnabled = ImportButton.IsEnabled = ConflictsButton.IsEnabled = !value;
        AccountProgress.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ShowSession()
    {
        SignedInPanel.Visibility = session is null ? Visibility.Collapsed : Visibility.Visible;
        SignedOutPanel.Visibility = session is null ? Visibility.Visible : Visibility.Collapsed;
        UserName.Text = session?.User.Name ?? "";
        UserEmail.Text = session?.User.Email ?? "";
    }

    private async Task SelectScopeAsync()
    {
        repository!.SelectAccount(session is null ? null : server, session?.User.Id);
        ShowSession();
        ConflictsButton.Visibility = Visibility.Collapsed;
        SyncStatus.Text = session is null ? "" : "Usando o cache desta conta. Aguardando sincronização.";
        if (changed is not null) await changed(true);
        SessionChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task RestoreAsync()
    {
        if (busy || session is null || api is null) return;
        SetBusy(true);
        RetryButton.Visibility = Visibility.Collapsed;
        try
        {
            await EnsureAccessAsync();
            try { session = session with { User = await api.MeAsync(session.AccessToken) }; }
            catch (AccountApiException ex) when (ex.Unauthorized)
            { await RefreshAsync(); session = session with { User = await api.MeAsync(session!.AccessToken) }; }
            await store!.SaveAsync(session);
            ShowSession(); AccountStatus.Text = "Conta conectada.";
            await SynchronizeCoreAsync();
        }
        catch (Exception ex) { await HandleErrorAsync(ex); }
        finally { SetBusy(false); }
    }

    private async Task RefreshAsync()
    {
        session = await api!.RefreshAsync(session!.RefreshToken);
        await store!.SaveAsync(session);
    }
    private async Task EnsureAccessAsync()
    {
        if (session!.AccessExpiresAt <= DateTimeOffset.UtcNow.AddSeconds(30)) await RefreshAsync();
    }
    private async Task<string> AccessTokenAsync() { await EnsureAccessAsync(); return session!.AccessToken; }

    private async Task SynchronizeCoreAsync()
    {
        var cache = repository!.Current;
        await new AlarmSyncEngine(cache, new AuthenticatedAlarmApi(api!, AccessTokenAsync, RefreshAsync, WaitForEditorAsync)).SyncAsync();
        var pending = await cache.GetPendingAsync();
        var conflicts = await cache.GetConflictsAsync();
        ConflictsButton.Visibility = conflicts.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        SyncStatus.Text = conflicts.Count > 0 ? $"{conflicts.Count} alteração(ões) simultânea(s). Revise qual versão manter."
            : $"Sincronizado às {DateTime.Now:HH:mm}. {pending.Count} alteração(ões) aguardando envio.";
        if (changed is not null) await changed(false);
    }

    private async Task SynchronizeAsync()
    {
        if (busy || session is null || api is null || repository is null || !canSynchronize()) return;
        SetBusy(true);
        try { await SynchronizeCoreAsync(); RetryButton.Visibility = Visibility.Collapsed; }
        catch (Exception ex) { await HandleErrorAsync(ex); }
        finally { SetBusy(false); }
    }

    private async void Submit_Click(object sender, RoutedEventArgs e)
    {
        if (busy || api is null || store is null) return;
        if (RegisterToggle.IsOn && PasswordInput.Password != ConfirmInput.Password)
        { AccountStatus.Text = "As senhas devem ser iguais."; return; }
        SetBusy(true);
        try
        {
            session = RegisterToggle.IsOn
                ? await api.RegisterAsync(NameInput.Text.Trim(), EmailInput.Text.Trim(), PasswordInput.Password)
                : await api.LoginAsync(EmailInput.Text.Trim(), PasswordInput.Password);
            await SelectScopeAsync();
            await store.SaveAsync(session);
            AccountStatus.Text = "Conta conectada.";
            await SynchronizeCoreAsync();
        }
        catch (Exception ex) { await HandleErrorAsync(ex); }
        finally { PasswordInput.Password = ConfirmInput.Password = ""; SetBusy(false); }
    }

    private async void Logout_Click(object sender, RoutedEventArgs e)
    {
        if (busy || session is null || store is null || api is null) return;
        SetBusy(true);
        var revoked = false;
        try { await EnsureAccessAsync(); await api.LogoutAsync(session.AccessToken); revoked = true; }
        catch (AccountApiException ex) when (ex.Unauthorized) { revoked = true; }
        catch (Exception) { }
        try
        {
            store.Clear(); session = null; await SelectScopeAsync();
            RetryButton.Visibility = Visibility.Collapsed;
            AccountStatus.Text = revoked ? "Você saiu da conta. Os alarmes desta conta voltam ao entrar novamente."
                : "Você saiu neste computador. Não foi possível encerrar a sessão no servidor.";
        }
        catch (Exception ex) { ReportError(ex); }
        finally { SetBusy(false); }
    }

    private async void Import_Click(object sender, RoutedEventArgs e)
    {
        if (busy || session is null || repository is null) return;
        SetBusy(true);
        try
        {
            var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = "Adicionar alarmes à conta?",
                Content = $"Copiar os alarmes usados sem login para {session.User.Email}? Os originais serão preservados. Os sons personalizados permanecem neste computador.",
                PrimaryButtonText = "Adicionar à conta", CloseButtonText = "Cancelar", DefaultButton = ContentDialogButton.Close };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
            var count = await repository.ImportLocalAsync();
            AccountStatus.Text = $"{count} alarme(s) adicionado(s) à conta.";
            if (changed is not null) await changed(false);
            await SynchronizeCoreAsync();
        }
        catch (Exception ex) { await HandleErrorAsync(ex); }
        finally { SetBusy(false); }
    }

    private async void Conflicts_Click(object sender, RoutedEventArgs e)
    {
        if (busy || session is null || repository is null) return;
        SetBusy(true);
        try
        {
            var cache = repository.Current;
            foreach (var conflict in await cache.GetConflictsAsync())
            {
                var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = "Alteração simultânea: " + conflict.Title,
                    Content = conflict.Remote.Deleted ? "Este alarme foi excluído em outro dispositivo. Escolha manter a exclusão ou enviar sua versão local."
                        : $"Outro dispositivo alterou este alarme. Versão do servidor: {conflict.Remote.Alarm!.Title}, {conflict.Remote.Alarm.ScheduledAtUtc.ToLocalTime():dd/MM/yyyy HH:mm}. Escolha qual versão manter.",
                    PrimaryButtonText = "Usar servidor", SecondaryButtonText = "Manter minhas alterações", CloseButtonText = "Decidir depois" };
                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.None) break;
                await cache.ResolveConflictAsync(conflict.Id, result == ContentDialogResult.Secondary);
            }
            if (changed is not null) await changed(false);
            await SynchronizeCoreAsync();
        }
        catch (Exception ex) { await HandleErrorAsync(ex); }
        finally { SetBusy(false); }
    }

    private async Task HandleErrorAsync(Exception ex)
    {
        if (ex is AccountApiException { Unauthorized: true })
        {
            try { store?.Clear(); } catch (Exception) { }
            session = null; await SelectScopeAsync();
        }
        ReportError(ex);
        RetryButton.Visibility = session is null ? Visibility.Collapsed : Visibility.Visible;
        if (session is not null) SyncStatus.Text = "Sem sincronização. Alterações locais serão enviadas na próxima tentativa.";
    }
    private async Task WaitForEditorAsync()
    {
        while (!canSynchronize())
        {
            if (shutdown) throw new OperationCanceledException();
            await Task.Delay(100);
        }
    }
    private async void Retry_Click(object sender, RoutedEventArgs e) => await RestoreAsync();
    private async void Sync_Click(object sender, RoutedEventArgs e) => await SynchronizeAsync();

    private void ReportError(Exception ex) => AccountStatus.Text = ex switch
    {
        AccountApiException error => error.Message,
        HttpRequestException or TaskCanceledException => "Não foi possível conectar. Seus alarmes em cache continuam funcionando.",
        IOException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException => "Não foi possível salvar ou ler sua sessão neste computador.",
        _ => "Não foi possível concluir a solicitação. Tente novamente."
    };
}
