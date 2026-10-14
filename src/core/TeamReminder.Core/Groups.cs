namespace TeamReminder.Core;

public enum GroupRole { Member = 0, Administrator = 1, Owner = 2 }
public static class GroupRoles
{
    public static string Label(GroupRole role) => role switch
    { GroupRole.Owner => "Proprietário", GroupRole.Administrator => "Administrador", _ => "Membro" };
}
public sealed record GroupCreateRequest(string? Name, string? Description);
public sealed record GroupEditRequest(string? Name, string? Description, long ExpectedRevision);
public sealed record GroupRevisionRequest(long ExpectedRevision);
public sealed record GroupInviteRequest(string? Email, long ExpectedRevision);
public sealed record GroupRoleRequest(GroupRole Role, long ExpectedRevision);
public sealed record GroupTransferRequest(Guid AccountId, long ExpectedRevision);
public sealed record GroupSummary(Guid Id, string Name, string Description, Guid OwnerAccountId, GroupRole MyRole, long Revision, int MemberCount)
{
    public string RoleLabel => GroupRoles.Label(MyRole);
    public string MembershipLabel => $"{RoleLabel} · {MemberCount} participante(s)";
}
public sealed record GroupMemberResponse(Guid AccountId, string Name, string Email, GroupRole Role, DateTimeOffset JoinedAt)
{
    public string RoleLabel => GroupRoles.Label(Role);
}
public sealed record GroupInvitationResponse(Guid Id, Guid GroupId, string GroupName, string Email, string InvitedByName, DateTimeOffset ExpiresAt, string Status)
{
    public string Details => $"Convite de {InvitedByName} · válido até {ExpiresAt.ToLocalTime():dd/MM/yyyy HH:mm}";
}
public sealed record GroupDetails(GroupSummary Group, IReadOnlyList<GroupMemberResponse> Members, IReadOnlyList<GroupInvitationResponse> Invitations);
public sealed record GroupAuditResponse(Guid Id, Guid ActorAccountId, string ActorName, Guid? TargetAccountId, string Action, DateTimeOffset CreatedAt)
{
    public string Description => $"{CreatedAt.ToLocalTime():dd/MM/yyyy HH:mm} · {ActorName} · " + (Action switch
    {
        "AlarmCreated" => "publicou um alarme", "AlarmUpdated" => "editou um alarme", "AlarmDeleted" => "excluiu um alarme",
        "Created" => "criou o grupo", "Updated" => "editou o grupo", "Invited" => "convidou um participante",
        "InvitationAccepted" => "aceitou um convite", "InvitationDeclined" => "recusou um convite",
        "InvitationCanceled" => "cancelou um convite", "Promoted" => "promoveu um administrador",
        "Demoted" => "alterou um administrador para membro", "Removed" => "removeu um participante",
        "Left" => "saiu do grupo", "OwnershipTransferred" => "transferiu a propriedade", _ => Action
    });
}
