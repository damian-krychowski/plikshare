using System.ComponentModel;
using PlikShare.Agents.Id;
using PlikShare.Users.Cache;
using PlikShare.Users.Id;

namespace PlikShare.Agents.Cache;

[ImmutableObject(true)]
public sealed class AgentContext
{
    public required int Id { get; init; }
    public required AgentExtId ExternalId { get; init; }
    public required string Name { get; init; }
    public required bool IsEnabled { get; init; }
    public required AgentOwnerContext Owner { get; init; }

    public required int? MaxWorkspaceNumber { get; init; }
    public required long? DefaultMaxWorkspaceSizeInBytes { get; init; }
    public required int? DefaultMaxWorkspaceTeamMembers { get; init; }
    public required UserStorageAccess StorageAccess { get; init; }

    /// <summary>
    /// Per-tool overrides explicitly set for this agent, keyed by tool name. A missing entry
    /// means "use the catalog default" — resolution happens in <c>AgentToolCatalog.Resolve</c>.
    /// </summary>
    public required IReadOnlyDictionary<string, AgentToolConfigEntry> ToolConfigs { get; init; }

    public bool CanAccessStorage(int storageId) => StorageAccess.Allows(storageId);
}

public sealed class AgentOwnerContext
{
    public required int Id { get; init; }
    public required UserExtId ExternalId { get; init; }
}

public sealed class AgentToolConfigEntry
{
    public required bool IsEnabled { get; init; }
    public required bool RequiresApproval { get; init; }
}
