using PlikShare.Users.StorageAccess;

namespace PlikShare.Agents.UpdateSettings.Contracts;

public class UpdateAgentMaxWorkspaceNumberRequestDto
{
    public required int? MaxWorkspaceNumber { get; init; }
}

public class UpdateAgentDefaultMaxWorkspaceSizeRequestDto
{
    public required long? MaxSizeInBytes { get; init; }
}

public class UpdateAgentDefaultMaxWorkspaceTeamMembersRequestDto
{
    public required int? MaxTeamMembers { get; init; }
}

public class UpdateAgentStorageAccessRequestDto
{
    public required UserStorageAccessMode Mode { get; init; }
    public required List<string> StorageExternalIds { get; init; }
}
