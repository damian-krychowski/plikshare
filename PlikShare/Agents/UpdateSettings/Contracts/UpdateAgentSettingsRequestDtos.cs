using PlikShare.Users.StorageAccess;

namespace PlikShare.Agents.UpdateSettings.Contracts;

public class UpdateAgentPermissionsAndRolesRequestDto
{
    public required bool IsAdmin { get; init; }
    public required bool CanAddWorkspace { get; init; }
    public required bool CanManageGeneralSettings { get; init; }
    public required bool CanManageUsers { get; init; }
    public required bool CanManageStorages { get; init; }
    public required bool CanManageEmailProviders { get; init; }
    public required bool CanManageAuth { get; init; }
    public required bool CanManageIntegrations { get; init; }
    public required bool CanManageAuditLog { get; init; }
}

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
