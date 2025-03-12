using Microsoft.Data.Sqlite;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Dashboard.Content.Contracts;
using PlikShare.Users.Cache;
using static PlikShare.Dashboard.Content.Contracts.GetDashboardContentResponseDto;

namespace PlikShare.Dashboard.Content;

public class GetDashboardContentQuery(PlikShareDb plikShareDb)
{
    public GetDashboardContentResponseDto Execute(
        UserContext user)
    {
        using var connection = plikShareDb.OpenConnection();

        var storages = GetStorages(
            user,
            connection);
        
        var (workspaces, otherWorkspaces, workspaceInvitations) =  GetWorkspaces(
            user, 
            connection);
        
        var (boxes, boxInvitations) = GetBoxes(
            user, 
            connection);

        return new GetDashboardContentResponseDto
        {
            Storages = storages,
            Workspaces = workspaces,
            OtherWorkspaces = otherWorkspaces,
            WorkspaceInvitations = workspaceInvitations,
            Boxes = boxes,
            BoxInvitations = boxInvitations
        };
    }

    private static List<Storage> GetStorages(
        UserContext user,
        SqliteConnection connection)
    {
        return connection
            .Cmd(
                sql: """
                     SELECT
                         s_external_id,
                         s_name,
                         s_type,
                         (CASE
                             WHEN $isUserAdmin = TRUE THEN (
                                 SELECT COUNT(*) FROM w_workspaces WHERE w_storage_id = s_id
                             )
                         END) AS s_workspaces_count,
                         (CASE
                             WHEN s_encryption_type IS NULL THEN 'none'
                             ELSE s_encryption_type
                         END) AS s_encryption_type
                     FROM s_storages
                     ORDER BY s_id ASC
                     """,
                readRowFunc: reader => new Storage
                {
                    ExternalId = reader.GetString(0),
                    Name = reader.GetString(1),
                    Type = reader.GetString(2),
                    WorkspacesCount = reader.GetInt32OrNull(3),
                    EncryptionType = reader.GetString(4)
                })
            .WithParameter("$isUserAdmin", user.Roles.IsAppOwner)
            .Execute();
    }
    
    private static BoxesResult GetBoxes(
        UserContext user, 
        SqliteConnection connection)
    {
        var result = new BoxesResult(
            Boxes: [],
            BoxInvitations: []);

        var boxes =  connection
            .Cmd<object>(
                sql: """
                     SELECT
                         bo_external_id,
                         bo_name,
                         bm_allow_download,
                         bm_allow_upload,
                         bm_allow_list,
                         bm_allow_delete_file,
                         bm_allow_rename_file,
                         bm_allow_move_items,
                         bm_allow_create_folder,
                         bm_allow_delete_folder,
                         bm_allow_rename_folder,
                         owner.u_email AS bm_owner_email,
                         owner.u_external_id AS bm_owner_external_id,
                         inviter.u_email AS bm_inviter_email,
                         inviter.u_external_id AS bm_inviter_external_id,
                         (CASE
                             WHEN bm_was_invitation_accepted = TRUE THEN 1
                             ELSE 2
                         END) AS bm_type_delimiter_value
                     FROM bm_box_membership
                     INNER JOIN bo_boxes
                         ON bo_id = bm_box_id
                         AND bo_is_being_deleted = FALSE
                     INNER JOIN w_workspaces
                         ON w_id = bo_workspace_id
                     INNER JOIN u_users AS owner
                         ON owner.u_id = w_owner_id
                     INNER JOIN u_users AS inviter
                         ON inviter.u_id = bm_inviter_id
                     WHERE
                         bm_member_id = $userId
                     ORDER BY 
                         bm_was_invitation_accepted ASC,
                         bo_id ASC
                     """,
                readRowFunc: reader =>
                {
                    var externalId = reader.GetString(0);
                    var name = reader.GetString(1);

                    var boxPermissions = new BoxPermissions
                    {
                        AllowDownload = reader.GetBoolean(2),
                        AllowUpload = reader.GetBoolean(3),
                        AllowList = reader.GetBoolean(4),
                        AllowDeleteFile = reader.GetBoolean(5),
                        AllowRenameFile = reader.GetBoolean(6),
                        AllowMoveItems = reader.GetBoolean(7),
                        AllowCreateFolder = reader.GetBoolean(8),
                        AllowDeleteFolder = reader.GetBoolean(9),
                        AllowRenameFolder = reader.GetBoolean(10)
                    };

                    var owner = new User
                    {
                        Email = reader.GetString(11),
                        ExternalId = reader.GetString(12)
                    };

                    var inviter = new User
                    {
                        Email = reader.GetString(13),
                        ExternalId = reader.GetString(14)
                    };

                    var typeDelimiter = reader.GetInt32(15);

                    if (typeDelimiter == 1)
                    {
                        return new ExternalBox
                        {
                            BoxExternalId = externalId,
                            BoxName = name,
                            Owner = owner,
                            Permissions = boxPermissions
                        };
                    }

                    return new ExternalBoxInvitation
                    {
                        BoxExternalId = externalId,
                        BoxName = name,
                        Inviter = inviter,
                        Owner = owner,
                        Permissions = boxPermissions
                    };
                })
            .WithParameter("$userId", user.Id)
            .Execute();
        
        foreach (var box in boxes)
        {
            switch (box)
            {
                case ExternalBox externalBox:
                    result.Boxes.Add(externalBox);
                    break;

                case ExternalBoxInvitation externalBoxInvitation:
                    result.BoxInvitations.Add(externalBoxInvitation);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(box), box.GetType(), "Unknown box type");
            }
        }

        return result;
    }

    private static WorkspacesResult GetWorkspaces(
        UserContext user,
        SqliteConnection connection)
    {
        var result = new WorkspacesResult(
            Workspaces: [],
            OtherWorkspaces: [],
            WorkspaceInvitations: []);

        var entities = connection
            .Cmd(
                sql: @"
                    SELECT 
                        w_external_id,
                        w_name,
                        w_current_size_in_bytes,
                        owner.u_email AS w_owner_email,
                        owner.u_external_id AS w_owner_external_id,
                        inviter.u_email AS w_inviter_email,
                        inviter.u_external_id AS w_inviter_external_id,
                        (CASE
                            WHEN (w_owner_id = $userId OR $isUserAdmin = TRUE) THEN TRUE
                            ELSE wm_allow_share
                        END) AS w_allow_share,
                        (CASE
                            WHEN $isUserAdmin = TRUE THEN storage.s_name
                        END) AS w_storage_name,
                        (
                            SELECT EXISTS (       
                                SELECT 1
                                FROM i_integrations
                                WHERE i_workspace_id = w_id
                            )                        
                        ) AS w_is_used_by_integration,
                        w_is_bucket_created,
                        (CASE
                            WHEN w_owner_id = $userId THEN 1
                            WHEN w_owner_id != $userId AND wm_was_invitation_accepted = TRUE THEN 1
                            WHEN w_owner_id != $userId AND wm_was_invitation_accepted = FALSE THEN 2
                            ELSE 3
                        END) AS w_type_delimiter_value
                    FROM w_workspaces
                    INNER JOIN u_users AS owner
                        ON owner.u_id = w_owner_id
                    INNER JOIN s_storages AS storage
                        ON storage.s_id = w_storage_id
                    LEFT JOIN wm_workspace_membership
                        ON wm_workspace_id = w_id 
                        AND wm_member_id = $userId
                    LEFT JOIN u_users AS inviter
                        ON inviter.u_id = wm_inviter_id
                    WHERE 
                        w_is_being_deleted = FALSE
                        AND ((w_owner_id = $userId OR wm_member_id = $userId) OR ($isUserAdmin = TRUE))
                    ORDER BY 
                        w_type_delimiter_value ASC,
                        wm_was_invitation_accepted ASC,
                        w_id ASC
                ",
                readRowFunc: reader => new
                {
                    ExternalId = reader.GetString(0),
                    Name = reader.GetString(1),
                    CurrentSizeInBytes = reader.GetInt64(2),
                    Owner = new User
                    {
                        Email = reader.GetString(3),
                        ExternalId = reader.GetString(4),
                    },
                    InviterEmail = reader.GetStringOrNull(5),
                    InviterExternalId = reader.GetStringOrNull(6),
                    Permissions = new WorkspacePermissions
                    {
                        AllowShare = reader.GetBoolean(7)
                    },
                    StorageName = reader.GetStringOrNull(8),
                    IsUsedByIntegration = reader.GetBoolean(9),
                    IsBucketCreated = reader.GetBoolean(10),
                    TypeDelimiter = reader.GetInt32(11)
                })
            .WithParameter("$userId", user.Id)
            .WithParameter("$isUserAdmin", user.Roles.IsAppOwner || user.Roles.IsAdmin)
            .WithParameter("$userExternalId", user.ExternalId.Value)
            .Execute();

        foreach (var entity in entities)
        {
            switch (entity.TypeDelimiter)
            {
                case 1:
                    result.Workspaces.Add(new WorkspaceDetails
                    {
                        ExternalId = entity.ExternalId,
                        Name = entity.Name,
                        CurrentSizeInBytes = entity.CurrentSizeInBytes,
                        Owner = entity.Owner,
                        StorageName = entity.StorageName,
                        Permissions = entity.Permissions,
                        IsUsedByIntegration = entity.IsUsedByIntegration,
                        IsBucketCreated = entity.IsBucketCreated
                    });
                    break;

                case 2:
                    result.WorkspaceInvitations.Add(new WorkspaceInvitation
                    {
                        WorkspaceExternalId = entity.ExternalId,
                        WorkspaceName = entity.Name,
                        Owner = entity.Owner,
                        Inviter = entity.InviterExternalId is null || entity.InviterEmail is null
                            ? null
                            : new User
                            {
                                ExternalId = entity.InviterExternalId,
                                Email = entity.InviterEmail
                            },
                        Permissions = entity.Permissions,
                        StorageName = entity.StorageName,
                        IsUsedByIntegration = entity.IsUsedByIntegration,
                        IsBucketCreated = entity.IsBucketCreated
                    });
                    break;

                case 3:
                    result.OtherWorkspaces.Add(new WorkspaceDetails
                    {
                        ExternalId = entity.ExternalId,
                        Name = entity.Name,
                        CurrentSizeInBytes = entity.CurrentSizeInBytes,
                        Owner = entity.Owner,
                        StorageName = entity.StorageName,
                        Permissions = entity.Permissions,
                        IsUsedByIntegration = entity.IsUsedByIntegration,
                        IsBucketCreated = entity.IsBucketCreated
                    });
                    break;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(entity.TypeDelimiter), 
                        entity.TypeDelimiter,
                        "Unknown workspace type delimiter");
            }
        }

        return result;
    }

    private readonly record struct WorkspacesResult(
        List<WorkspaceDetails> Workspaces,
        List<WorkspaceDetails> OtherWorkspaces,
        List<WorkspaceInvitation> WorkspaceInvitations);

    private readonly record struct BoxesResult(
        List<ExternalBox> Boxes,
        List<ExternalBoxInvitation> BoxInvitations);
}