using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Storages.Encryption;
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

        var isFullEncrypted = workspace.Storage.Encryption.Type == StorageEncryptionType.Full;

        var memberships = connection
            .Cmd(
                sql: """
                     SELECT
                         um.u_external_id AS w_member_external_id,
                         um.u_email AS w_member_email,
                         wm_was_invitation_accepted,
                         wm_allow_share,
                         ui.u_email AS w_inviter_email,
                         (
                             $isFullEncrypted = TRUE
                             AND NOT EXISTS (
                                 SELECT 1
                                 FROM wek_workspace_encryption_keys
                                 WHERE wek_workspace_id = $workspaceId
                                   AND wek_user_id = wm_member_id
                             )
                             AND NOT EXISTS (
                                 SELECT 1
                                 FROM sek_storage_encryption_keys
                                 WHERE sek_storage_id = $storageId
                                   AND sek_user_id = wm_member_id
                             )
                         ) AS w_is_pending_key_grant
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
                    InviterEmail = reader.GetStringOrNull(4),
                    IsPendingKeyGrant = reader.GetBoolean(5)
                })
            .WithParameter("$workspaceId", workspace.Id)
            .WithParameter("$storageId", workspace.Storage.StorageId)
            .WithParameter("$isFullEncrypted", isFullEncrypted)
            .Execute();

        return new GetWorkspaceMembersListResponseDto
        {
            Items = memberships
        };
    }
}
