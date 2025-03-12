using PlikShare.Users.Id;

namespace PlikShare.Account.Contracts;

public record GetAccountDetailsResponseDto(
    UserExtId ExternalId,
    string Email,
    GetAccountRolesResponseDto Roles,
    GetAccountPermissionsResponseDto Permissions);
    
public record GetAccountRolesResponseDto(
    bool IsAppOwner,
    bool IsAdmin);
    
public record GetAccountPermissionsResponseDto(
    bool CanAddWorkspace,
    bool CanManageGeneralSettings,
    bool CanManageUsers,
    bool CanManageStorages,
    bool CanManageEmailProviders);