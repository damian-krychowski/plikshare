using Microsoft.Data.Sqlite;
using PlikShare.Boxes.Id;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Integrations;
using PlikShare.Users.Cache;
using PlikShare.Users.GetDetails.Contracts;
using PlikShare.Users.Id;
using PlikShare.Workspaces.Id;

namespace PlikShare.Users.GetDetails;

public class GetUserDetailsQuery(PlikShareDb plikShareDb)
{
    public GetUserDetails.ResponseDto Execute(
        UserContext user)
    {
        using var connection = plikShareDb.OpenConnection();

        var workspaces = GetWorkspaces(
            user: user,
            connection: connection);
        
        var sharedWorkspaces = GetSharedWorkspaces(
            user: user,
            connection: connection);

        var sharedBoxes = GetSharedBoxes(
            user: user,
            connection: connection);
        
        return new GetUserDetails.ResponseDto
        {
            User = new GetUserDetails.UserDetailsDto
            {
                ExternalId = user.ExternalId,
                Email = user.Email.Value,
                IsEmailConfirmed = user.IsEmailConfirmed,
                Roles = new GetUserDetails.UserRolesDto
                {
                    IsAppOwner = user.Roles.IsAppOwner,
                    IsAdmin = user.Roles.IsAdmin
                },
                Permissions = new GetUserDetails.UserPermissionsDto
                {
                    CanAddWorkspace = user.Permissions.CanAddWorkspace,
                    CanManageGeneralSettings = user.Permissions.CanManageGeneralSettings,
                    CanManageUsers = user.Permissions.CanManageUsers,
                    CanManageStorages = user.Permissions.CanManageStorages,
                    CanManageEmailProviders = user.Permissions.CanManageEmailProviders
                },
                MaxWorkspaceNumber = user.MaxWorkspaceNumber,
                DefaultMaxWorkspaceSizeInBytes = user.DefaultMaxWorkspaceSizeInBytes,
                DefaultMaxWorkspaceTeamMembers = user.DefaultMaxWorkspaceTeamMembers
            },
            Workspaces = workspaces,
            SharedWorkspaces = sharedWorkspaces,
            SharedBoxes = sharedBoxes
        };
    }
    
     private static List<GetUserDetails.WorkspaceDto> GetWorkspaces(
        UserContext user,
        SqliteConnection connection)
     {
         return connection
             .Cmd(
                 sql: """
                      SELECT 
                          w_external_id,
                          w_name,
                          w_current_size_in_bytes,
                          w_max_size_in_bytes,
                          (
                              SELECT EXISTS (       
                                  SELECT 1
                                  FROM i_integrations
                                  WHERE i_workspace_id = w_id
                              )                        
                          ) AS w_is_used_by_integration,
                          w_is_bucket_created,
                          storage.s_name AS w_storage_name
                      FROM w_workspaces
                      INNER JOIN s_storages AS storage
                          ON storage.s_id = w_storage_id
                      WHERE 
                          w_is_being_deleted = FALSE
                          AND w_owner_id = $userId
                      ORDER BY 
                          w_id ASC
                      """,
                 readRowFunc: reader => new GetUserDetails.WorkspaceDto
                 {
                     ExternalId = reader.GetExtId<WorkspaceExtId>(0),
                     Name = reader.GetString(1),
                     CurrentSizeInBytes = reader.GetInt64(2),
                     MaxSizeInBytes = reader.GetInt64OrNull(3),
                     IsUsedByIntegration = reader.GetBoolean(4),
                     IsBucketCreated = reader.GetBoolean(5),
                     StorageName = reader.GetString(6)
                 }
             )
             .WithParameter("$userId", user.Id)
             .Execute();
     }

     private static List<GetUserDetails.SharedWorkspaceDto> GetSharedWorkspaces(
         UserContext user,
         SqliteConnection connection)
     {
         return connection
             .Cmd(
                 sql: """
                      SELECT 
                          w_external_id,
                          w_name,
                          w_current_size_in_bytes,
                          w_max_size_in_bytes,
                          storage.s_name AS w_storage_name,
                          owner.u_email AS w_owner_email,
                          owner.u_external_id AS w_owner_external_id,
                          inviter.u_email AS w_inviter_email,
                          inviter.u_external_id AS w_inviter_external_id, 
                          wm_allow_share,
                          wm_was_invitation_accepted,
                          (
                              SELECT EXISTS (       
                                  SELECT 1
                                  FROM i_integrations
                                  WHERE i_workspace_id = w_id
                              )
                          
                          ) AS w_is_used_by_integration,
                          w_is_bucket_created
                      FROM wm_workspace_membership
                      INNER JOIN w_workspaces
                          ON w_id = wm_workspace_id
                      INNER JOIN u_users AS owner
                          ON owner.u_id = w_owner_id
                      INNER JOIN s_storages AS storage
                          ON storage.s_id = w_storage_id
                      INNER JOIN u_users AS inviter
                          ON inviter.u_id = wm_inviter_id
                      WHERE 
                          w_is_being_deleted = FALSE
                          AND wm_member_id = $userId
                      ORDER BY 
                          w_id ASC
                      """,
                 readRowFunc: reader => new GetUserDetails.SharedWorkspaceDto
                 {
                     ExternalId = reader.GetExtId<WorkspaceExtId>(0),
                     Name = reader.GetString(1),
                     CurrentSizeInBytes = reader.GetInt64(2),
                     MaxSizeInBytes = reader.GetInt64OrNull(3),
                     StorageName = reader.GetString(4),
                     Owner = new GetUserDetails.UserDto
                     {
                         Email = reader.GetString(5),
                         ExternalId = reader.GetExtId<UserExtId>(6)
                     },
                     Inviter = new GetUserDetails.UserDto
                     {
                         Email = reader.GetString(7),
                         ExternalId = reader.GetExtId<UserExtId>(8)
                     },
                     Permissions = new GetUserDetails.WorkspacePermissionsDto
                     {
                         AllowShare = reader.GetBoolean(9)
                     },
                     WasInvitationAccepted = reader.GetBoolean(10),
                     IsUsedByIntegration = reader.GetBoolean(11),
                     IsBucketCreated = reader.GetBoolean(12)
                 }
             )
             .WithParameter("$userId", user.Id)
             .WithEnumParameter("$textractIntegrationType", IntegrationType.AwsTextract)
             .WithEnumParameter("$chatGptIntegrationType", IntegrationType.OpenaiChatgpt)
             .Execute();
     }
     
     private static List<GetUserDetails.SharedBoxDto> GetSharedBoxes(
         UserContext user,
         SqliteConnection connection)
     {
         return connection
             .Cmd(
                 sql: @"
                    SELECT
                        w_external_id,
                        w_name,
                        storage.s_name AS w_storage_name,
                        owner.u_email AS w_owner_email,
                        owner.u_external_id AS w_owner_external_id,
                        bo_external_id,
                        bo_name,
                        inviter.u_email AS bm_inviter_email,
                        inviter.u_external_id AS bm_inviter_external_id, 
                        bm_allow_download,
                        bm_allow_upload,
                        bm_allow_list,
                        bm_allow_delete_file,
                        bm_allow_rename_file,
                        bm_allow_move_items,
                        bm_allow_create_folder,
                        bm_allow_delete_folder,
                        bm_allow_rename_folder,
                        bm_was_invitation_accepted
                    FROM bm_box_membership
                    INNER JOIN bo_boxes
                        ON bo_id = bm_box_id
                    INNER JOIN w_workspaces
                        ON w_id = bo_workspace_id                        
                    INNER JOIN s_storages AS storage
                        ON storage.s_id = w_storage_id
                    INNER JOIN u_users AS owner
                        ON owner.u_id = w_owner_id
                    INNER JOIN u_users AS inviter
                        ON inviter.u_id = bm_inviter_id
                    WHERE
                        bo_is_being_deleted = FALSE
                        AND w_is_being_deleted = FALSE
                        AND bm_member_id = $userId
                    ORDER BY 
                        bo_id ASC
                ",
                 readRowFunc: reader => new GetUserDetails.SharedBoxDto
                 {
                     WorkspaceExternalId = reader.GetExtId<WorkspaceExtId>(0),
                     WorkspaceName = reader.GetString(1),
                     StorageName = reader.GetString(2),
                     Owner = new GetUserDetails.UserDto
                     {
                         Email = reader.GetString(3),
                         ExternalId = reader.GetExtId<UserExtId>(4),
                     },
                     BoxExternalId = reader.GetExtId<BoxExtId>(5),
                     BoxName = reader.GetString(6),
                     Inviter = new GetUserDetails.UserDto
                     {
                         Email = reader.GetString(7),
                         ExternalId = reader.GetExtId<UserExtId>(8),
                     },
                     Permissions = new GetUserDetails.BoxPermissionsDto
                     {
                         AllowDownload = reader.GetBoolean(9),
                         AllowUpload = reader.GetBoolean(10),
                         AllowList = reader.GetBoolean(11),
                         AllowDeleteFile = reader.GetBoolean(12),
                         AllowRenameFile = reader.GetBoolean(13),
                         AllowMoveItems = reader.GetBoolean(14),
                         AllowCreateFolder = reader.GetBoolean(15),
                         AllowDeleteFolder = reader.GetBoolean(16),
                         AllowRenameFolder = reader.GetBoolean(17),
                     },
                     WasInvitationAccepted = reader.GetBoolean(18)
                 }
             )
             .WithParameter("$userId", user.Id)
             .Execute();
     }
}