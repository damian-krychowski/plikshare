using PlikShare.Boxes.Id;
using PlikShare.Users.Id;
using PlikShare.Workspaces.Id;

namespace PlikShare.Users.GetDetails.Contracts;

public static class GetUserDetails
{
    public record ResponseDto(
        UserDetailsDto User,
        List<WorkspaceDto> Workspaces,
        List<SharedWorkspaceDto> SharedWorkspaces,
        List<SharedBoxDto> SharedBoxes);
    
    public record UserDetailsDto(
        UserExtId ExternalId,
        string Email,
        bool IsEmailConfirmed,
        UserRolesDto Roles,
        UserPermissionsDto Permissions);
    
    public record UserRolesDto(
        bool IsAppOwner,
        bool IsAdmin);
    
    public record UserPermissionsDto(
        bool CanAddWorkspace,
        bool CanManageGeneralSettings,
        bool CanManageUsers,
        bool CanManageStorages,
        bool CanManageEmailProviders);
    
    public record WorkspaceDto(
        WorkspaceExtId ExternalId,
        string Name,
        string StorageName,
        long CurrentSizeInBytes,
        bool IsUsedByIntegration,
        bool IsBucketCreated);
    
    public record SharedWorkspaceDto(
        WorkspaceExtId ExternalId,
        string Name,
        string StorageName,
        long CurrentSizeInBytes, 
        UserDto Owner,
        UserDto Inviter,
        bool WasInvitationAccepted,
        WorkspacePermissionsDto Permissions,
        bool IsUsedByIntegration,
        bool IsBucketCreated);
    
    public record SharedBoxDto(
        WorkspaceExtId WorkspaceExternalId,
        string WorkspaceName,
        string StorageName,
        UserDto Owner,
        BoxExtId BoxExternalId,
        string BoxName,
        UserDto Inviter,
        bool WasInvitationAccepted,
        BoxPermissionsDto Permissions);
    
    public record WorkspacePermissionsDto(
        bool AllowShare);
    
    public record BoxPermissionsDto(
        bool AllowDownload,
        bool AllowUpload,
        bool AllowList,
        bool AllowDeleteFile,
        bool AllowRenameFile,
        bool AllowMoveItems,
        bool AllowCreateFolder,
        bool AllowRenameFolder,
        bool AllowDeleteFolder);

    public record UserDto(
        UserExtId ExternalId,
        string Email);
}

