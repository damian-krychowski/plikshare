using PlikShare.Agents.Id;
using PlikShare.Boxes.Id;
using PlikShare.Storages.Id;
using PlikShare.Users.Id;
using PlikShare.Users.StorageAccess;
using PlikShare.Workspaces.Id;

namespace PlikShare.Agents.Get.Contracts;

public static class GetAgentDetails
{
    public class ResponseDto
    {
        public required AgentDetailsDto Agent { get; init; }
        public required List<WorkspaceDto> OwnedWorkspaces { get; init; }
        public required List<SharedWorkspaceDto> SharedWorkspaces { get; init; }
        public required List<SharedBoxDto> SharedBoxes { get; init; }
    }

    public class AgentDetailsDto
    {
        public required AgentExtId ExternalId { get; init; }
        public required string Name { get; init; }
        public required bool IsEnabled { get; init; }
        public required DateTimeOffset CreatedAt { get; init; }
        public required OwnerDto Owner { get; init; }
        public required string TokenMasked { get; init; }
        public required DateTimeOffset? TokenLastUsedAt { get; init; }
        public required AgentRolesDto Roles { get; init; }
        public required AgentPermissionsDto Permissions { get; init; }
        public required int? MaxWorkspaceNumber { get; init; }
        public required long? DefaultMaxWorkspaceSizeInBytes { get; init; }
        public required int? DefaultMaxWorkspaceTeamMembers { get; init; }
        public required StorageAccessDto StorageAccess { get; init; }
    }

    public class OwnerDto
    {
        public required UserExtId ExternalId { get; init; }
        public required string Email { get; init; }
    }

    public class AgentRolesDto
    {
        public required bool IsAdmin { get; init; }
    }

    public class AgentPermissionsDto
    {
        public required bool CanAddWorkspace { get; init; }
        public required bool CanManageGeneralSettings { get; init; }
        public required bool CanManageUsers { get; init; }
        public required bool CanManageStorages { get; init; }
        public required bool CanManageEmailProviders { get; init; }
        public required bool CanManageAuth { get; init; }
        public required bool CanManageIntegrations { get; init; }
        public required bool CanManageAuditLog { get; init; }
        public required bool CanManageAgents { get; init; }
    }

    public class StorageAccessDto
    {
        public required UserStorageAccessMode Mode { get; init; }
        public required List<string> StorageExternalIds { get; init; }
    }

    public class WorkspaceDto
    {
        public required WorkspaceExtId ExternalId { get; init; }
        public required string Name { get; init; }
        public required long CurrentSizeInBytes { get; init; }
        public required long? MaxSizeInBytes { get; init; }
        public required bool IsBucketCreated { get; init; }
        public required string StorageName { get; init; }
    }

    public class SharedWorkspaceDto
    {
        public required WorkspaceExtId ExternalId { get; init; }
        public required string Name { get; init; }
        public required StorageExtId StorageExternalId { get; init; }
        public required string StorageName { get; init; }
        public required long CurrentSizeInBytes { get; init; }
        public required long? MaxSizeInBytes { get; init; }
        public required OwnerDto Owner { get; init; }
        public required bool IsBucketCreated { get; init; }
        public required string StorageEncryptionType { get; init; }
    }

    public class SharedBoxDto
    {
        public required WorkspaceExtId WorkspaceExternalId { get; init; }
        public required string WorkspaceName { get; init; }
        public required string StorageName { get; init; }
        public required OwnerDto Owner { get; init; }
        public required BoxExtId BoxExternalId { get; init; }
        public required string BoxName { get; init; }
        public required BoxPermissionsDto Permissions { get; init; }
    }

    public class BoxPermissionsDto
    {
        public required bool AllowDownload { get; init; }
        public required bool AllowUpload { get; init; }
        public required bool AllowList { get; init; }
        public required bool AllowDeleteFile { get; init; }
        public required bool AllowRenameFile { get; init; }
        public required bool AllowMoveItems { get; init; }
        public required bool AllowCreateFolder { get; init; }
        public required bool AllowRenameFolder { get; init; }
        public required bool AllowDeleteFolder { get; init; }
    }
}
