using PlikShare.Users.Id;

namespace PlikShare.Users.List.Contracts;

public class GetUsersResponseDto
{
    public required List<GetUsersItemDto> Items { get; init; }
}

public class GetUsersItemDto
{
    public required UserExtId ExternalId { get; init; }
    public required string Email { get; init; }
    public required bool IsEmailConfirmed { get; init; }
    public required int WorkspacesCount { get; init; }
    public required GetUserItemRolesDto Roles { get; init; }
    public required GetUserItemPermissionsDto Permissions { get; init; }
    public required int? MaxWorkspaceNumber { get; init; }
    public required long? DefaultMaxWorkspaceSizeInBytes { get; init; }
    public required int? DefaultMaxWorkspaceTeamMembers { get; init; }
}

public class GetUserItemRolesDto
{
    public required bool IsAppOwner { get; init; }
    public required bool IsAdmin { get; init; }
}

public class GetUserItemPermissionsDto
{
    public required bool CanAddWorkspace { get; init; }
    public required bool CanManageGeneralSettings { get; init; }
    public required bool CanManageUsers { get; init; }
    public required bool CanManageStorages { get; init; }
    public required bool CanManageEmailProviders { get; init; }
}