using PlikShare.Users.Cache;
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
    public required UserRoles Roles { get; init; }
    public required UserPermissions Permissions { get; init; }
    public required int? MaxWorkspaceNumber { get; init; }
    public required long? DefaultMaxWorkspaceSizeInBytes { get; init; }
    public required int? DefaultMaxWorkspaceTeamMembers { get; init; }
}