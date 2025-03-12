using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Users.Id;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.Members.List.Contracts;

namespace PlikShare.Workspaces.Members.List;

public class GetWorkspaceMembersListQuery(PlikShareDb plikShareDb)
{
    public GetWorkspaceMembersListResponseDto Execute(
        WorkspaceContext workspace,
        CancellationToken cancellationToken = default)
    {
        using var connection = plikShareDb.OpenConnection();

        var memberships = connection
            .Cmd(
                sql: """
                     SELECT
                         um.u_external_id AS w_member_external_id,
                         um.u_email AS w_member_email,
                         wm_was_invitation_accepted,
                         wm_allow_share,
                         ui.u_email AS w_inviter_email
                     FROM wm_workspace_membership
                     INNER JOIN u_users AS ui
                         ON ui.u_id = wm_inviter_id
                     LEFT JOIN u_users AS um
                         ON um.u_id = wm_member_id
                     WHERE
                         wm_workspace_id = $workspaceId
                     """,
                readRowFunc: reader => new GetWorkspaceMembersListResponseDto.Membership
                {
                    MemberExternalId = reader.GetExtId<UserExtId>(0),
                    MemberEmail = reader.GetString(1),
                    WasInvitationAccepted = reader.GetBoolean(2),
                    Permissions = new GetWorkspaceMembersListResponseDto.WorkspacePermissions
                    {
                        AllowShare = reader.GetBoolean(3)
                    },
                    InviterEmail = reader.GetStringOrNull(4)
                })
            .WithParameter("$workspaceId", workspace.Id)
            .Execute();

        return new GetWorkspaceMembersListResponseDto
        {
            Items = memberships
        };
    }
}