using PlikShare.Users.Id;

namespace PlikShare.Users.List.Contracts;

public record GetUsersResponseDto(
    GetUsersItemDto[] Items);

public record GetUsersItemDto(
    UserExtId ExternalId,
    string Email,
    bool IsEmailConfirmed,
    int WorkspacesCount,
    GetUserItemRolesDto Roles,
    GetUserItemPermissionsDto Permissions);

public record GetUserItemRolesDto(
    bool IsAppOwner,
    bool IsAdmin); 
    
public record GetUserItemPermissionsDto(
    bool CanAddWorkspace,
    bool CanManageGeneralSettings,
    bool CanManageUsers,
    bool CanManageStorages,
    bool CanManageEmailProviders);