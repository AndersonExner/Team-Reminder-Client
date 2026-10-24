using System.Net.Http;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TeamReminder.Core;

namespace TeamReminder.Windows;

public sealed partial class GroupsControl : UserControl
{
    private AccountControl? account;
    private GroupDetails? details;
    private bool busy;
    private bool updatingSelection;
    private int generation;
    public bool IsActive { get; set; }
    public GroupsControl() => InitializeComponent();
    public void Configure(AccountControl connection)
    {
        account = connection;
        account.SessionChanged += (_, _) =>
        {
            generation++; Clear();
            if (IsActive && account.HasSession) _ = RefreshAsync();
        };
        Clear();
    }

    private void Clear()
    {
        updatingSelection = true;
        try { GroupList.ItemsSource = null; IncomingList.ItemsSource = null; SetDetails(null); }
        finally { updatingSelection = false; }
        EmptyGroups.Visibility = EmptyInvitations.Visibility = Visibility.Visible;
        StatusText.Text = "";
        UpdatePermissions();
    }
    private void SetDetails(GroupDetails? value)
    {
        details = value;
        SelectedName.Text = value?.Group.Name ?? "Selecione um grupo";
        SelectedDescription.Text = value?.Group.Description ?? "";
        SelectedRole.Text = value?.Group.MembershipLabel ?? "";
        MembersList.ItemsSource = value?.Members;
        OutgoingList.ItemsSource = value?.Invitations;
        HistoryList.ItemsSource = null;
        InviteEmail.Text = "";
        UpdatePermissions();
    }
    private void UpdatePermissions()
    {
        if (CreateButton is null || MembersList is null || Progress is null) return;
        var connected = account?.HasSession == true;
        var enabled = connected && !busy;
        var administrator = details?.Group.MyRole is GroupRole.Administrator or GroupRole.Owner;
        var owner = details?.Group.MyRole == GroupRole.Owner;
        var member = MembersList.SelectedItem as GroupMemberResponse;
        LoginHint.Visibility = connected ? Visibility.Collapsed : Visibility.Visible;
        ConnectedContent.Visibility = connected ? Visibility.Visible : Visibility.Collapsed;
        CreateButton.IsEnabled = RefreshButton.IsEnabled = GroupList.IsEnabled = IncomingList.IsEnabled = enabled;
        MembersList.IsEnabled = OutgoingList.IsEnabled = enabled;
        AcceptButton.IsEnabled = DeclineButton.IsEnabled = enabled && IncomingList.SelectedItem is GroupInvitationResponse;
        EditButton.IsEnabled = enabled && administrator;
        LeaveButton.IsEnabled = enabled && details is not null && !owner;
        DeleteButton.IsEnabled = enabled && owner;
        AdminPanel.Visibility = administrator ? Visibility.Visible : Visibility.Collapsed;
        InviteButton.IsEnabled = InviteEmail.IsEnabled = HistoryButton.IsEnabled = enabled && administrator;
        PromoteButton.IsEnabled = enabled && administrator && member?.Role == GroupRole.Member;
        DemoteButton.IsEnabled = enabled && administrator && member?.Role == GroupRole.Administrator;
        RemoveButton.IsEnabled = enabled && administrator && member is not null && member.Role != GroupRole.Owner;
        TransferButton.IsEnabled = enabled && owner && member is not null && member.Role != GroupRole.Owner;
        CancelInviteButton.IsEnabled = enabled && administrator && OutgoingList.SelectedItem is GroupInvitationResponse;
        Progress.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
    }
    private async Task RunAsync(Func<Task> operation)
    {
        if (busy || account is null) return;
        busy = true; UpdatePermissions();
        try { await operation(); }
        catch (Exception ex)
        {
            StatusText.Text = ex switch
            {
                AccountApiException error => error.Message,
                HttpRequestException or TaskCanceledException => "Não foi possível conectar. Os grupos exigem conexão; seus alarmes locais continuam funcionando.",
                _ => "Não foi possível concluir a ação. Atualize os grupos e tente novamente."
            };
        }
        finally { busy = false; UpdatePermissions(); }
    }
    private Task<T> Execute<T>(Func<AuthClient, string, Task<T>> operation) => account!.ExecuteAuthenticatedAsync(operation);

    public Task RefreshAsync() => RunAsync(async () =>
    {
        if (account?.HasSession != true) { Clear(); return; }
        await ReloadAsync(); StatusText.Text = "Grupos e convites atualizados.";
    });
    private async Task ReloadAsync(Guid? select = null)
    {
        var expectedGeneration = generation;
        var selected = select ?? (GroupList.SelectedItem as GroupSummary)?.Id;
        var groups = await Execute((api, token) => api.GroupsAsync(token));
        var invitations = await Execute((api, token) => api.InvitationsAsync(token));
        if (expectedGeneration != generation) return;
        updatingSelection = true;
        try
        {
            GroupList.ItemsSource = groups; IncomingList.ItemsSource = invitations;
            GroupList.SelectedItem = groups.FirstOrDefault(x => x.Id == selected);
            SetDetails(null);
        }
        finally { updatingSelection = false; }
        EmptyGroups.Visibility = groups.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyInvitations.Visibility = invitations.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (GroupList.SelectedItem is GroupSummary group)
        {
            var loaded = await Execute((api, token) => api.GroupAsync(group.Id, token));
            if (expectedGeneration == generation) SetDetails(loaded);
        }
    }
    private async void Group_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (updatingSelection || GroupList.SelectedItem is not GroupSummary group) return;
        await RunAsync(async () =>
        {
            var expected = generation;
            var loaded = await Execute((api, token) => api.GroupAsync(group.Id, token));
            if (expected == generation) SetDetails(loaded);
        });
    }
    private void Selection_Changed(object sender, SelectionChangedEventArgs e) => UpdatePermissions();
    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

    private async Task<(string Name, string Description)?> EditDialog(string title, string name = "", string description = "")
    {
        var nameBox = new TextBox { Header = "Nome do grupo", Text = name, MaxLength = 100 };
        var descriptionBox = new TextBox { Header = "Descrição", Text = description, MaxLength = 500, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap };
        var panel = new StackPanel { Spacing = 12 }; panel.Children.Add(nameBox); panel.Children.Add(descriptionBox);
        var dialog = new ContentDialog { XamlRoot = XamlRoot, Title = title, Content = panel,
            PrimaryButtonText = "Salvar", CloseButtonText = "Cancelar", DefaultButton = ContentDialogButton.Close };
        return await dialog.ShowAsync() == ContentDialogResult.Primary ? (nameBox.Text.Trim(), descriptionBox.Text.Trim()) : null;
    }
    private async Task<bool> Confirm(string title, string message, string action) =>
        await new ContentDialog { XamlRoot = XamlRoot, Title = title, Content = message, PrimaryButtonText = action,
            CloseButtonText = "Cancelar", DefaultButton = ContentDialogButton.Close }.ShowAsync() == ContentDialogResult.Primary;

    private async void Create_Click(object sender, RoutedEventArgs e) => await RunAsync(async () =>
    {
        var fields = await EditDialog("Criar grupo"); if (fields is null) return;
        var group = await Execute((api, token) => api.CreateGroupAsync(new(fields.Value.Name, fields.Value.Description), token));
        await ReloadAsync(group.Id); StatusText.Text = "Grupo criado. Você é o proprietário.";
    });
    private async void Edit_Click(object sender, RoutedEventArgs e) => await RunAsync(async () =>
    {
        if (details is not { } selected) return;
        var fields = await EditDialog("Editar grupo", selected.Group.Name, selected.Group.Description); if (fields is null) return;
        await Execute((api, token) => api.EditGroupAsync(selected.Group.Id, new(fields.Value.Name, fields.Value.Description, selected.Group.Revision), token));
        await ReloadAsync(selected.Group.Id); StatusText.Text = "Grupo atualizado.";
    });
    private async void Invite_Click(object sender, RoutedEventArgs e) => await RunAsync(async () =>
    {
        if (details is not { } selected) return;
        await Execute((api, token) => api.InviteToGroupAsync(selected.Group.Id, new(InviteEmail.Text.Trim(), selected.Group.Revision), token));
        await ReloadAsync(selected.Group.Id); StatusText.Text = "Convite enviado. A pessoa precisa aceitá-lo no aplicativo.";
    });
    private async void Accept_Click(object sender, RoutedEventArgs e) => await RunAsync(async () =>
    {
        if (IncomingList.SelectedItem is not GroupInvitationResponse invite) return;
        await Execute((api, token) => api.AcceptGroupInvitationAsync(invite.Id, token));
        await ReloadAsync(invite.GroupId); StatusText.Text = "Convite aceito. Você entrou como membro.";
    });
    private async void Decline_Click(object sender, RoutedEventArgs e) => await RunAsync(async () =>
    {
        if (IncomingList.SelectedItem is not GroupInvitationResponse invite) return;
        if (!await Confirm("Recusar convite?", invite.GroupName, "Recusar")) return;
        await Execute((api, token) => api.DeclineGroupInvitationAsync(invite.Id, token));
        await ReloadAsync(); StatusText.Text = "Convite recusado.";
    });
    private async void CancelInvite_Click(object sender, RoutedEventArgs e) => await RunAsync(async () =>
    {
        if (details is not { } selected || OutgoingList.SelectedItem is not GroupInvitationResponse invite) return;
        if (!await Confirm("Cancelar convite?", invite.Email, "Cancelar convite")) return;
        await Execute((api, token) => api.CancelGroupInvitationAsync(selected.Group.Id, invite.Id, selected.Group.Revision, token));
        await ReloadAsync(selected.Group.Id); StatusText.Text = "Convite cancelado.";
    });
    private async Task ChangeRole(GroupRole role)
    {
        if (details is not { } selected || MembersList.SelectedItem is not GroupMemberResponse member) return;
        if (!await Confirm("Alterar papel?", $"{member.Name} passará a ser {GroupRoles.Label(role).ToLowerInvariant()}. Ao retirar a administração, os convites pendentes enviados por essa pessoa são cancelados.", "Alterar")) return;
        await Execute((api, token) => api.ChangeGroupRoleAsync(selected.Group.Id, member.AccountId, new(role, selected.Group.Revision), token));
        await ReloadAsync(selected.Group.Id); StatusText.Text = "Papel atualizado.";
    }
    private async void Promote_Click(object sender, RoutedEventArgs e) => await RunAsync(() => ChangeRole(GroupRole.Administrator));
    private async void Demote_Click(object sender, RoutedEventArgs e) => await RunAsync(() => ChangeRole(GroupRole.Member));
    private async void Remove_Click(object sender, RoutedEventArgs e) => await RunAsync(async () =>
    {
        if (details is not { } selected || MembersList.SelectedItem is not GroupMemberResponse member) return;
        if (!await Confirm("Remover participante?", $"Remover {member.Name} de {selected.Group.Name}?", "Remover")) return;
        await Execute((api, token) => api.RemoveGroupMemberAsync(selected.Group.Id, member.AccountId, selected.Group.Revision, token));
        await ReloadAsync(selected.Group.Id); StatusText.Text = "Participante removido.";
    });
    private async void Transfer_Click(object sender, RoutedEventArgs e) => await RunAsync(async () =>
    {
        if (details is not { } selected || MembersList.SelectedItem is not GroupMemberResponse member) return;
        if (!await Confirm("Transferir propriedade?", $"{member.Name} será o proprietário. Você continuará como administrador.", "Transferir")) return;
        await Execute((api, token) => api.TransferGroupAsync(selected.Group.Id, new(member.AccountId, selected.Group.Revision), token));
        await ReloadAsync(selected.Group.Id); StatusText.Text = "Propriedade transferida.";
    });
    private async void Leave_Click(object sender, RoutedEventArgs e) => await RunAsync(async () =>
    {
        if (details is not { } selected) return;
        if (!await Confirm("Sair do grupo?", $"Você sairá de {selected.Group.Name}. Para voltar, precisará de um novo convite.", "Sair")) return;
        await Execute((api, token) => api.LeaveGroupAsync(selected.Group.Id, selected.Group.Revision, token));
        await ReloadAsync(); StatusText.Text = "Você saiu do grupo.";
    });
    private async void Delete_Click(object sender, RoutedEventArgs e) => await RunAsync(async () =>
    {
        if (details is not { } selected) return;
        if (!await Confirm("Excluir grupo?", $"{selected.Group.Name} ficará indisponível para todos e seus convites serão cancelados.", "Excluir grupo")) return;
        await Execute((api, token) => api.DeleteGroupAsync(selected.Group.Id, selected.Group.Revision, token));
        await ReloadAsync(); StatusText.Text = "Grupo excluído.";
    });
    private async void History_Click(object sender, RoutedEventArgs e) => await RunAsync(async () =>
    {
        if (details is not { } selected) return;
        var expected = generation;
        var history = await Execute((api, token) => api.GroupAuditAsync(selected.Group.Id, token));
        if (expected == generation) HistoryList.ItemsSource = history;
    });
}
