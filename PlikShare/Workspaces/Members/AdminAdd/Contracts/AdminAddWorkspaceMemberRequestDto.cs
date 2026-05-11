using PlikShare.Users.Id;

namespace PlikShare.Workspaces.Members.AdminAdd.Contracts;

public class AdminAddWorkspaceMemberRequestDto
{
    public required UserExtId MemberExternalId { get; init; }
    public required bool AllowShare { get; init; }
}

public class AdminAddWorkspaceMemberResponseDto
{
    public required string Email { get; init; }
    public required UserExtId ExternalId { get; init; }
}
