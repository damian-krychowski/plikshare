using System.ComponentModel;
using PlikShare.Agents.Id;
using PlikShare.Users.Cache;

namespace PlikShare.Agents.Cache;

[ImmutableObject(true)]
public sealed class AgentContext
{
    public required int Id { get; init; }
    public required AgentExtId ExternalId { get; init; }
    public required string Name { get; init; }
    public required bool IsEnabled { get; init; }
    public required AgentRoles Roles { get; init; }
    public required AgentPermissions Permissions { get; init; }
    public required int? MaxWorkspaceNumber { get; init; }
    public required long? DefaultMaxWorkspaceSizeInBytes { get; init; }
    public required int? DefaultMaxWorkspaceTeamMembers { get; init; }
    public required UserStorageAccess StorageAccess { get; init; }

    public bool HasAdminRole => Roles.IsAdmin;

    public bool CanAccessStorage(int storageId)
    {
        if (Roles.IsAdmin)
            return true;

        return StorageAccess.Allows(storageId);
    }
}

public sealed class AgentRoles
{
    public required bool IsAdmin { get; init; }
}

public sealed class AgentPermissions
{
    public required bool CanAddWorkspace { get; init; }
    public required bool CanManageGeneralSettings { get; init; }
    public required bool CanManageUsers { get; init; }
    public required bool CanManageStorages { get; init; }
    public required bool CanManageEmailProviders { get; init; }
    public required bool CanManageAuth { get; init; }
    public required bool CanManageIntegrations { get; init; }
    public required bool CanManageAuditLog { get; init; }
    public required bool CanManageAgents { get; init; }
}
