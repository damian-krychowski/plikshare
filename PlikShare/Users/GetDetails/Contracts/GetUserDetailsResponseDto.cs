using PlikShare.Boxes.Id;
using PlikShare.Users.Id;
using PlikShare.Workspaces.Id;

namespace PlikShare.Users.GetDetails.Contracts;

public static class GetUserDetails
{
    public class ResponseDto
    {
        public required UserDetailsDto User { get; init; }
        public required List<WorkspaceDto> Workspaces { get; init; }
        public required List<SharedWorkspaceDto> SharedWorkspaces { get; init; }
        public required List<SharedBoxDto> SharedBoxes { get; init; }
    }

    public class UserDetailsDto
    {
        public required UserExtId ExternalId { get; init; }
        public required string Email { get; init; }
        public required bool IsEmailConfirmed { get; init; }
        public required UserRolesDto Roles { get; init; }
        public required UserPermissionsDto Permissions { get; init; }
        public required int? MaxWorkspaceNumber { get; init; }
        public required long? DefaultMaxWorkspaceSizeInBytes { get; init; }
        public required int? DefaultMaxWorkspaceTeamMembers { get; init; }
    }

    public class UserRolesDto
    {
        public required bool IsAppOwner { get; init; }
        public required bool IsAdmin { get; init; }
    }


    public class UserPermissionsDto
    {
        public required bool CanAddWorkspace { get; init; }
        public required bool CanManageGeneralSettings { get; init; }
        public required bool CanManageUsers { get; init; }
        public required bool CanManageStorages { get; init; }
        public required bool CanManageEmailProviders { get; init; }
    }


    public class WorkspaceDto
    {
        public required WorkspaceExtId ExternalId { get; init; }
        public required string Name { get; init; }
        public required long CurrentSizeInBytes { get; init; }
        public required long? MaxSizeInBytes { get; init; }
        public required bool IsUsedByIntegration { get; init; }
        public required bool IsBucketCreated { get; init; }
        public required string StorageName { get; init; }
    }

    public class SharedWorkspaceDto
    {
        public required WorkspaceExtId ExternalId { get; init; }
        public required string Name { get; init; }
        public required string StorageName { get; init; }
        public required long CurrentSizeInBytes { get; init; }
        public required long? MaxSizeInBytes { get; init; }
        public required UserDto Owner { get; init; }
        public required UserDto Inviter { get; init; }
        public required bool WasInvitationAccepted { get; init; }
        public required WorkspacePermissionsDto Permissions { get; init; }
        public required bool IsUsedByIntegration { get; init; }
        public required bool IsBucketCreated { get; init; }
    }

    public class SharedBoxDto
    {
        public required WorkspaceExtId WorkspaceExternalId { get; init; }
        public required string WorkspaceName { get; init; }
        public required string StorageName { get; init; }
        public required UserDto Owner { get; init; }
        public required BoxExtId BoxExternalId { get; init; }
        public required string BoxName { get; init; }
        public required UserDto Inviter { get; init; }
        public required bool WasInvitationAccepted { get; init; }
        public required BoxPermissionsDto Permissions { get; init; }
    }

    public class WorkspacePermissionsDto
    {
        public required bool AllowShare { get; init; }
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

    public class UserDto
    {
        public required UserExtId ExternalId { get; init; }
        public required string Email { get; init; }
    }
}