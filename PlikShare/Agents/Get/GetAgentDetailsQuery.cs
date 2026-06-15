using Microsoft.Data.Sqlite;
using PlikShare.Agents.Get.Contracts;
using PlikShare.Agents.Id;
using PlikShare.Boxes.Id;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Storages.Id;
using PlikShare.Users.Id;
using PlikShare.Users.StorageAccess;
using PlikShare.Workspaces.Id;

namespace PlikShare.Agents.Get;

public class GetAgentDetailsQuery(PlikShareDb plikShareDb)
{
    public GetAgentDetails.ResponseDto? Execute(
        AgentExtId externalId)
    {
        using var connection = plikShareDb.OpenConnection();

        var agentRow = ReadAgent(connection, externalId);

        if (agentRow is null)
            return null;

        var agent = agentRow.Value;

        var token = ReadToken(connection, agent.Id);

        var storageAccessExternalIds = agent.StorageAccessMode == UserStorageAccessMode.All
            ? new List<string>()
            : ReadStorageAccessExternalIds(connection, agent.Id);

        return new GetAgentDetails.ResponseDto
        {
            Agent = new GetAgentDetails.AgentDetailsDto
            {
                ExternalId = externalId,
                Name = agent.Name,
                IsEnabled = agent.IsEnabled,
                CreatedAt = agent.CreatedAt,
                Owner = new GetAgentDetails.OwnerDto
                {
                    ExternalId = agent.OwnerExternalId,
                    Email = agent.OwnerEmail
                },
                TokenMasked = token?.Masked ?? "",
                TokenLastUsedAt = token?.LastUsedAt,
                Roles = new GetAgentDetails.AgentRolesDto
                {
                    IsAdmin = agent.IsAdmin
                },
                Permissions = new GetAgentDetails.AgentPermissionsDto
                {
                    CanAddWorkspace = agent.CanAddWorkspace,
                    CanManageGeneralSettings = agent.CanManageGeneralSettings,
                    CanManageUsers = agent.CanManageUsers,
                    CanManageStorages = agent.CanManageStorages,
                    CanManageEmailProviders = agent.CanManageEmailProviders,
                    CanManageAuth = agent.CanManageAuth,
                    CanManageIntegrations = agent.CanManageIntegrations,
                    CanManageAuditLog = agent.CanManageAuditLog,
                    CanManageAgents = agent.CanManageAgents
                },
                MaxWorkspaceNumber = agent.MaxWorkspaceNumber,
                DefaultMaxWorkspaceSizeInBytes = agent.DefaultMaxWorkspaceSizeInBytes,
                DefaultMaxWorkspaceTeamMembers = agent.DefaultMaxWorkspaceTeamMembers,
                StorageAccess = new GetAgentDetails.StorageAccessDto
                {
                    Mode = agent.StorageAccessMode,
                    StorageExternalIds = storageAccessExternalIds
                }
            },
            OwnedWorkspaces = ReadOwnedWorkspaces(connection, agent.Id),
            SharedWorkspaces = ReadSharedWorkspaces(connection, agent.Id),
            SharedBoxes = ReadSharedBoxes(connection, agent.Id)
        };
    }

    private static AgentRow? ReadAgent(SqliteConnection connection, AgentExtId externalId)
    {
        var result = connection
            .OneRowCmd(
                sql: """
                     SELECT
                         a_id,
                         a_name,
                         a_is_enabled,
                         a_created_at,
                         a_is_admin,
                         a_can_add_workspace,
                         a_can_manage_general_settings,
                         a_can_manage_users,
                         a_can_manage_storages,
                         a_can_manage_email_providers,
                         a_can_manage_auth,
                         a_can_manage_integrations,
                         a_can_manage_audit_log,
                         a_can_manage_agents,
                         a_max_workspace_number,
                         a_default_max_workspace_size_in_bytes,
                         a_default_max_workspace_team_members,
                         a_storage_access_mode,
                         owner.u_external_id,
                         owner.u_email
                     FROM a_agents
                     INNER JOIN u_users AS owner
                         ON owner.u_id = a_owner_user_id
                     WHERE a_external_id = $externalId
                     LIMIT 1
                     """,
                readRowFunc: reader => new AgentRow(
                    Id: reader.GetInt32(0),
                    Name: reader.GetString(1),
                    IsEnabled: reader.GetBoolean(2),
                    CreatedAt: reader.GetFieldValue<DateTimeOffset>(3),
                    IsAdmin: reader.GetBoolean(4),
                    CanAddWorkspace: reader.GetBoolean(5),
                    CanManageGeneralSettings: reader.GetBoolean(6),
                    CanManageUsers: reader.GetBoolean(7),
                    CanManageStorages: reader.GetBoolean(8),
                    CanManageEmailProviders: reader.GetBoolean(9),
                    CanManageAuth: reader.GetBoolean(10),
                    CanManageIntegrations: reader.GetBoolean(11),
                    CanManageAuditLog: reader.GetBoolean(12),
                    CanManageAgents: reader.GetBoolean(13),
                    MaxWorkspaceNumber: reader.GetInt32OrNull(14),
                    DefaultMaxWorkspaceSizeInBytes: reader.GetInt64OrNull(15),
                    DefaultMaxWorkspaceTeamMembers: reader.GetInt32OrNull(16),
                    StorageAccessMode: reader.GetEnum<UserStorageAccessMode>(17),
                    OwnerExternalId: reader.GetExtId<UserExtId>(18),
                    OwnerEmail: reader.GetString(19)))
            .WithParameter("$externalId", externalId.Value)
            .Execute();

        return result.IsEmpty ? null : result.Value;
    }

    private static TokenRow? ReadToken(SqliteConnection connection, int agentId)
    {
        var result = connection
            .OneRowCmd(
                sql: """
                     SELECT
                         at_token_masked,
                         at_last_used_at
                     FROM at_agent_tokens
                     WHERE at_agent_id = $agentId
                         AND at_revoked_at IS NULL
                     ORDER BY at_id DESC
                     LIMIT 1
                     """,
                readRowFunc: reader => new TokenRow(
                    Masked: reader.GetString(0),
                    LastUsedAt: reader.GetDateTimeOffsetOrNull(1)))
            .WithParameter("$agentId", agentId)
            .Execute();

        return result.IsEmpty ? null : result.Value;
    }

    private static List<string> ReadStorageAccessExternalIds(SqliteConnection connection, int agentId)
    {
        return connection
            .Cmd(
                sql: """
                     SELECT s.s_external_id
                     FROM asa_agent_storage_access AS asa
                     INNER JOIN s_storages AS s
                         ON s.s_id = asa.asa_storage_id
                     WHERE asa.asa_agent_id = $agentId
                     ORDER BY s.s_id ASC
                     """,
                readRowFunc: reader => reader.GetExtId<StorageExtId>(0).Value)
            .WithParameter("$agentId", agentId)
            .Execute();
    }

    private static List<GetAgentDetails.WorkspaceDto> ReadOwnedWorkspaces(SqliteConnection connection, int agentId)
    {
        return connection
            .Cmd(
                sql: """
                     SELECT
                         w_external_id,
                         w_name,
                         w_current_size_in_bytes,
                         w_max_size_in_bytes,
                         w_is_bucket_created,
                         storage.s_name
                     FROM w_workspaces
                     INNER JOIN s_storages AS storage
                         ON storage.s_id = w_storage_id
                     WHERE w_is_being_deleted = FALSE
                         AND w_owner_agent_id = $agentId
                     ORDER BY w_id ASC
                     """,
                readRowFunc: reader => new GetAgentDetails.WorkspaceDto
                {
                    ExternalId = reader.GetExtId<WorkspaceExtId>(0),
                    Name = reader.GetString(1),
                    CurrentSizeInBytes = reader.GetInt64(2),
                    MaxSizeInBytes = reader.GetInt64OrNull(3),
                    IsBucketCreated = reader.GetBoolean(4),
                    StorageName = reader.GetString(5)
                })
            .WithParameter("$agentId", agentId)
            .Execute();
    }

    private static List<GetAgentDetails.SharedWorkspaceDto> ReadSharedWorkspaces(SqliteConnection connection, int agentId)
    {
        return connection
            .Cmd(
                sql: """
                     SELECT
                         w_external_id,
                         w_name,
                         w_current_size_in_bytes,
                         w_max_size_in_bytes,
                         storage.s_external_id,
                         storage.s_name,
                         storage.s_encryption_type,
                         w_is_bucket_created,
                         owner.u_external_id,
                         owner.u_email
                     FROM wa_workspace_agents
                     INNER JOIN w_workspaces
                         ON w_id = wa_workspace_id
                     INNER JOIN s_storages AS storage
                         ON storage.s_id = w_storage_id
                     INNER JOIN u_users AS owner
                         ON owner.u_id = w_owner_id
                     WHERE w_is_being_deleted = FALSE
                         AND wa_agent_id = $agentId
                     ORDER BY w_id ASC
                     """,
                readRowFunc: reader => new GetAgentDetails.SharedWorkspaceDto
                {
                    ExternalId = reader.GetExtId<WorkspaceExtId>(0),
                    Name = reader.GetString(1),
                    CurrentSizeInBytes = reader.GetInt64(2),
                    MaxSizeInBytes = reader.GetInt64OrNull(3),
                    StorageExternalId = reader.GetExtId<StorageExtId>(4),
                    StorageName = reader.GetString(5),
                    StorageEncryptionType = reader.GetString(6),
                    IsBucketCreated = reader.GetBoolean(7),
                    Owner = new GetAgentDetails.OwnerDto
                    {
                        ExternalId = reader.GetExtId<UserExtId>(8),
                        Email = reader.GetString(9)
                    }
                })
            .WithParameter("$agentId", agentId)
            .Execute();
    }

    private static List<GetAgentDetails.SharedBoxDto> ReadSharedBoxes(SqliteConnection connection, int agentId)
    {
        return connection
            .Cmd(
                sql: """
                     SELECT
                         w_external_id,
                         w_name,
                         storage.s_name,
                         owner.u_external_id,
                         owner.u_email,
                         bo_external_id,
                         bo_name,
                         ba_allow_download,
                         ba_allow_upload,
                         ba_allow_list,
                         ba_allow_delete_file,
                         ba_allow_rename_file,
                         ba_allow_move_items,
                         ba_allow_create_folder,
                         ba_allow_delete_folder,
                         ba_allow_rename_folder
                     FROM ba_box_agents
                     INNER JOIN bo_boxes
                         ON bo_id = ba_box_id
                     INNER JOIN w_workspaces
                         ON w_id = bo_workspace_id
                     INNER JOIN s_storages AS storage
                         ON storage.s_id = w_storage_id
                     INNER JOIN u_users AS owner
                         ON owner.u_id = w_owner_id
                     WHERE bo_is_being_deleted = FALSE
                         AND w_is_being_deleted = FALSE
                         AND ba_agent_id = $agentId
                     ORDER BY bo_id ASC
                     """,
                readRowFunc: reader => new GetAgentDetails.SharedBoxDto
                {
                    WorkspaceExternalId = reader.GetExtId<WorkspaceExtId>(0),
                    WorkspaceName = reader.GetString(1),
                    StorageName = reader.GetString(2),
                    Owner = new GetAgentDetails.OwnerDto
                    {
                        ExternalId = reader.GetExtId<UserExtId>(3),
                        Email = reader.GetString(4)
                    },
                    BoxExternalId = reader.GetExtId<BoxExtId>(5),
                    BoxName = reader.GetString(6),
                    Permissions = new GetAgentDetails.BoxPermissionsDto
                    {
                        AllowDownload = reader.GetBoolean(7),
                        AllowUpload = reader.GetBoolean(8),
                        AllowList = reader.GetBoolean(9),
                        AllowDeleteFile = reader.GetBoolean(10),
                        AllowRenameFile = reader.GetBoolean(11),
                        AllowMoveItems = reader.GetBoolean(12),
                        AllowCreateFolder = reader.GetBoolean(13),
                        AllowDeleteFolder = reader.GetBoolean(14),
                        AllowRenameFolder = reader.GetBoolean(15)
                    }
                })
            .WithParameter("$agentId", agentId)
            .Execute();
    }

    private readonly record struct AgentRow(
        int Id,
        string Name,
        bool IsEnabled,
        DateTimeOffset CreatedAt,
        bool IsAdmin,
        bool CanAddWorkspace,
        bool CanManageGeneralSettings,
        bool CanManageUsers,
        bool CanManageStorages,
        bool CanManageEmailProviders,
        bool CanManageAuth,
        bool CanManageIntegrations,
        bool CanManageAuditLog,
        bool CanManageAgents,
        int? MaxWorkspaceNumber,
        long? DefaultMaxWorkspaceSizeInBytes,
        int? DefaultMaxWorkspaceTeamMembers,
        UserStorageAccessMode StorageAccessMode,
        UserExtId OwnerExternalId,
        string OwnerEmail);

    private readonly record struct TokenRow(
        string Masked,
        DateTimeOffset? LastUsedAt);
}
