using System.Net.Http;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace TeamReminder.Windows;

public sealed partial class AccountControl : UserControl
{
    private AuthClient? api;
    private AccountSessionStore? store;
    private AccountSession? session;
    private bool initialized;
    private bool busy;

    public AccountControl() => InitializeComponent();

    private void Register_Toggled(object sender, RoutedEventArgs e)
    {
        if (NameInput is null) return;
        var registering = RegisterToggle.IsOn;
        NameInput.Visibility = ConfirmInput.Visibility = PasswordHint.Visibility = registering ? Visibility.Visible : Visibility.Collapsed;
        SubmitButton.Content = registering ? "Criar conta" : "Entrar";
        PasswordInput.Password = ConfirmInput.Password = "";
        AccountStatus.Text = "";
    }

    private async void Account_Loaded(object sender, RoutedEventArgs e)
    {
        if (initialized) return;
        initialized = true;
        try
        {
            var address = AuthClient.ServerAddress(Environment.GetEnvironmentVariable("TEAMREMINDER_API_URL"));
            api = new AuthClient(new HttpClient { BaseAddress = address, Timeout = TimeSpan.FromSeconds(60) });
            store = new AccountSessionStore(address.AbsoluteUri);
        }
        catch (Exception ex) when (ex is ArgumentException or UriFormatException)
        {
            SubmitButton.IsEnabled = false;
            AccountStatus.Text = "Não foi possível configurar a conexão com sua conta.";
            return;
        }
        await RestoreAsync();
    }

    private void SetBusy(bool value)
    {
        busy = value;
        SubmitButton.IsEnabled = RegisterToggle.IsEnabled = NameInput.IsEnabled = EmailInput.IsEnabled = PasswordInput.IsEnabled = ConfirmInput.IsEnabled = LogoutButton.IsEnabled = RetryButton.IsEnabled = !value;
        AccountProgress.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ShowSession()
    {
        SignedInPanel.Visibility = session is null ? Visibility.Collapsed : Visibility.Visible;
        SignedOutPanel.Visibility = session is null ? Visibility.Visible : Visibility.Collapsed;
        UserName.Text = session?.User.Name ?? "";
        UserEmail.Text = session?.User.Email ?? "";
    }

    private async Task RestoreAsync()
    {
        if (busy || api is null || store is null) return;
        SetBusy(true);
        RetryButton.Visibility = Visibility.Collapsed;
        try
        {
            session = await store.LoadAsync();
            if (session is null) { ShowSession(); AccountStatus.Text = ""; return; }
            if (session.RefreshExpiresAt <= DateTimeOffset.UtcNow)
            {
                store.Clear(); session = null; ShowSession();
                AccountStatus.Text = "Sua sessão expirou. Entre novamente.";
                return;
            }
            await EnsureAccessAsync();
            try { session = session with { User = await api.MeAsync(session.AccessToken) }; }
            catch (AccountApiException ex) when (ex.Unauthorized)
            {
                session = await api.RefreshAsync(session.RefreshToken);
                await store.SaveAsync(session);
                session = session with { User = await api.MeAsync(session.AccessToken) };
            }
            await store.SaveAsync(session);
            ShowSession(); AccountStatus.Text = "Conta conectada.";
        }
        catch (AccountApiException ex) when (ex.Unauthorized)
        {
            session = null;
            try { store.Clear(); } catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
            ShowSession(); AccountStatus.Text = "Sua sessão expirou. Entre novamente.";
        }
        catch (Exception ex)
        {
            ShowSession(); ReportError(ex);
            RetryButton.Visibility = Visibility.Visible;
        }
        finally { SetBusy(false); }
    }

    private async Task EnsureAccessAsync()
    {
        if (session!.AccessExpiresAt <= DateTimeOffset.UtcNow.AddSeconds(30))
        {
            session = await api!.RefreshAsync(session.RefreshToken);
            await store!.SaveAsync(session);
        }
    }

    private async void Submit_Click(object sender, RoutedEventArgs e)
    {
        if (busy || api is null || store is null) return;
        if (RegisterToggle.IsOn && PasswordInput.Password != ConfirmInput.Password)
        { AccountStatus.Text = "As senhas devem ser iguais."; return; }
        SetBusy(true);
        RetryButton.Visibility = Visibility.Collapsed;
        try
        {
            session = RegisterToggle.IsOn
                ? await api.RegisterAsync(NameInput.Text.Trim(), EmailInput.Text.Trim(), PasswordInput.Password)
                : await api.LoginAsync(EmailInput.Text.Trim(), PasswordInput.Password);
            ShowSession();
            await store.SaveAsync(session);
            AccountStatus.Text = "Conta conectada.";
        }
        catch (Exception ex) { ReportError(ex); }
        finally
        {
            PasswordInput.Password = ConfirmInput.Password = "";
            SetBusy(false);
        }
    }

    private async void Logout_Click(object sender, RoutedEventArgs e)
    {
        if (busy || session is null || store is null || api is null) return;
        SetBusy(true);
        var revoked = false;
        try
        {
            await EnsureAccessAsync();
            await api.LogoutAsync(session.AccessToken);
            revoked = true;
        }
        catch (AccountApiException ex) when (ex.Unauthorized) { revoked = true; }
        catch (Exception) { }
        try
        {
            store.Clear(); session = null; ShowSession();
            RetryButton.Visibility = Visibility.Collapsed;
            AccountStatus.Text = revoked ? "Você saiu da conta."
                : "Você saiu neste computador. Sem conexão, não foi possível encerrar a sessão no servidor.";
        }
        catch (Exception ex) { ReportError(ex); }
        finally { SetBusy(false); }
    }

    private async void Retry_Click(object sender, RoutedEventArgs e) => await RestoreAsync();

    private void ReportError(Exception ex) => AccountStatus.Text = ex switch
    {
        AccountApiException error => error.Message,
        HttpRequestException or TaskCanceledException => "Não foi possível conectar. Confira sua conexão e tente novamente. Seus alarmes locais continuam ativos.",
        IOException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException => "Não foi possível salvar ou ler sua sessão neste computador.",
        _ => "Não foi possível concluir a solicitação. Tente novamente."
    };
}
