using PlikShare.Users.Id;
using PlikShare.Users.PermissionsAndRoles;

namespace PlikShare.Users.Invite.Contracts;

public class InviteUsersRequestDto 
{
    public required List<string> Emails { get; init; }
}

public class InviteUsersResponseDto
{
    public required List<InvitedUserDto> Users { get; init; }
}

public class InvitedUserDto 
{
    public required string Email { get; init; }
    public required UserExtId ExternalId { get; init; }

    public required int? MaxWorkspaceNumber { get; init; }
    public required long? DefaultMaxWorkspaceSizeInBytes { get; init; }
    public required UserPermissionsAndRolesDto PermissionsAndRoles { get; init; }
};