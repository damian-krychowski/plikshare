using PlikShare.Users.Cache;
using PlikShare.Users.Id;

namespace PlikShare.Account.Contracts;

public class GetAccountDetailsResponseDto
{
    public required UserExtId ExternalId { get; init; }
    public required string Email { get; init; }
    public required UserRoles Roles { get; init; }
    public required UserPermissions Permissions { get; init; }
    public int? MaxWorkspaceNumber { get; init; }
}