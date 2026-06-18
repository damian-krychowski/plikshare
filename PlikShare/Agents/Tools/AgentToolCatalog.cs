using PlikShare.Agents.Cache;

namespace PlikShare.Agents.Tools;

/// <summary>
/// Agent permission that gates whether a tool is available at all (layer 1).
/// Per-tool enabled/disabled (layer 2) can only narrow within an available tool, never widen.
/// </summary>
public enum AgentToolPermission
{
    None = 0,
    AddWorkspace = 1
}

/// <summary>
/// How a tool is grouped in the configuration UI. <see cref="Instance"/> tools act at the
/// instance level (listing/creating workspaces); <see cref="Workspace"/> tools act on the
/// content of workspaces. This is a display concern only — whether a tool can be overridden
/// per workspace is a separate flag (<see cref="AgentToolDefinition.IsWorkspaceOverridable"/>).
/// </summary>
public enum AgentToolGroup
{
    Instance = 0,
    Workspace = 1
}

/// <summary>
/// Canonical definition of an agent tool: the single source of truth for its UI group, whether it
/// can carry a per-workspace override, the permission that gates it and the per-agent defaults used
/// when no explicit config row exists.
/// </summary>
public sealed record AgentToolDefinition(
    string Name,
    string Description,
    AgentToolGroup Group,
    bool IsWorkspaceOverridable,
    AgentToolPermission RequiredPermission,
    bool DefaultIsEnabled,
    bool DefaultRequiresApproval);

/// <summary>
/// A partial override at a single cascade level (workspace or box). A null dimension means
/// "inherit this dimension from the next level down"; each dimension cascades independently.
/// </summary>
public sealed record AgentToolScopeOverride(
    bool? IsEnabled,
    bool? RequiresApproval);

public sealed record EffectiveAgentToolConfig(
    bool IsAvailable,
    bool IsEnabled,
    bool RequiresApproval)
{
    public bool IsUsable => IsAvailable && IsEnabled;
}

public static class AgentToolCatalog
{
    // Every workspace-scoped tool can carry a per-workspace override. When the invocation names the
    // workspace explicitly (a workspaceExternalId argument) the gate resolves the override directly.
    // When it does not, the tool resolves the workspace itself before gating: file-id tools (get_file,
    // read_file, get_file_download_link) derive it from the file's workspace; search has no single
    // workspace, so it folds each workspace's override into the search instead — a workspace where
    // search is disabled drops out of the scope, and if any in-scope workspace requires approval the
    // whole search does. Only instance-level tools (listing/creating workspaces) are not overridable.
    public static readonly IReadOnlyList<AgentToolDefinition> All =
    [
        Read(AgentToolNames.ListWorkspaces, "Lists the workspaces the agent can access.", AgentToolGroup.Instance, overridable: false),
        Read(AgentToolNames.ListStorages, "Lists the storages the agent can create workspaces on.", AgentToolGroup.Instance, overridable: false),
        Write(AgentToolNames.CreateWorkspace, "Creates a new workspace owned by the agent.", AgentToolGroup.Instance, overridable: false, permission: AgentToolPermission.AddWorkspace),

        Read(AgentToolNames.GetFile, "Reads a single file's details — name, size, type and where it lives — by its id.", AgentToolGroup.Workspace, overridable: true),
        Read(AgentToolNames.ReadFile, "Reads the text content of a file.", AgentToolGroup.Workspace, overridable: true),
        Read(AgentToolNames.Search, "Searches for files and folders across the workspaces the agent can access.", AgentToolGroup.Workspace, overridable: true),
        Read(AgentToolNames.GetFileDownloadLink, "Creates a short-lived link to download a single file.", AgentToolGroup.Workspace, overridable: true),

        Read(AgentToolNames.ListWorkspaceContent, "Lists the folders and files inside a workspace or one of its folders.", AgentToolGroup.Workspace, overridable: true),
        Read(AgentToolNames.ListShareLinks, "Lists the public share links of a workspace.", AgentToolGroup.Workspace, overridable: true),
        Read(AgentToolNames.GetShareLink, "Reads the details of a single share link.", AgentToolGroup.Workspace, overridable: true),
        Read(AgentToolNames.GetBulkDownloadLink, "Creates a link to download several files or folders as one ZIP archive.", AgentToolGroup.Workspace, overridable: true),

        Write(AgentToolNames.RenameWorkspace, "Renames a workspace.", AgentToolGroup.Workspace, overridable: true),
        Write(AgentToolNames.CreateFile, "Creates a new text file.", AgentToolGroup.Workspace, overridable: true),
        Write(AgentToolNames.RenameFile, "Renames a file, keeping its extension.", AgentToolGroup.Workspace, overridable: true),
        Write(AgentToolNames.CreateFolder, "Creates a new folder.", AgentToolGroup.Workspace, overridable: true),
        Write(AgentToolNames.RenameFolder, "Renames a folder.", AgentToolGroup.Workspace, overridable: true),
        Write(AgentToolNames.MoveItems, "Moves files and folders into another folder.", AgentToolGroup.Workspace, overridable: true),
        Write(AgentToolNames.CreateShareLink, "Creates a public link that shares files or folders with anyone who has it.", AgentToolGroup.Workspace, overridable: true),
        Write(AgentToolNames.UpdateShareLink, "Changes a share link's settings — expiry, password and download limit.", AgentToolGroup.Workspace, overridable: true),

        Destructive(AgentToolNames.BulkDelete, "Deletes files and/or folders, including whole folder trees.", AgentToolGroup.Workspace, overridable: true),
        Destructive(AgentToolNames.DeleteShareLink, "Deletes a share link. The shared files and folders stay intact.", AgentToolGroup.Workspace, overridable: true)
    ];

    private static readonly IReadOnlyDictionary<string, AgentToolDefinition> ByName =
        All.ToDictionary(tool => tool.Name);

    public static IReadOnlyCollection<string> Names { get; } =
        All.Select(tool => tool.Name).ToHashSet();

    public static AgentToolDefinition? TryGet(string name) =>
        ByName.GetValueOrDefault(name);

    /// <summary>
    /// Resolves the effective config for a tool invocation by cascading, per dimension,
    /// box override → workspace override → agent global → catalog default. Availability is a
    /// global concern (permission); admin bypasses the permission but not a disabled flag.
    /// </summary>
    public static EffectiveAgentToolConfig Resolve(
        AgentContext agent,
        AgentToolDefinition definition,
        AgentToolScopeOverride? workspaceOverride = null,
        AgentToolScopeOverride? boxOverride = null)
    {
        var isAvailable = agent.HasAdminRole || definition.RequiredPermission switch
        {
            AgentToolPermission.None => true,
            AgentToolPermission.AddWorkspace => agent.Permissions.CanAddWorkspace,
            _ => false
        };

        var global = agent.ToolConfigs.GetValueOrDefault(definition.Name);

        var isEnabled =
            boxOverride?.IsEnabled
            ?? workspaceOverride?.IsEnabled
            ?? global?.IsEnabled
            ?? definition.DefaultIsEnabled;

        var requiresApproval =
            boxOverride?.RequiresApproval
            ?? workspaceOverride?.RequiresApproval
            ?? global?.RequiresApproval
            ?? definition.DefaultRequiresApproval;

        return new EffectiveAgentToolConfig(
            IsAvailable: isAvailable,
            IsEnabled: isEnabled,
            RequiresApproval: requiresApproval);
    }

    private static AgentToolDefinition Read(
        string name,
        string description,
        AgentToolGroup group,
        bool overridable,
        AgentToolPermission permission = AgentToolPermission.None) =>
        new(name, description, group, overridable, permission, DefaultIsEnabled: true, DefaultRequiresApproval: false);

    private static AgentToolDefinition Write(
        string name,
        string description,
        AgentToolGroup group,
        bool overridable,
        AgentToolPermission permission = AgentToolPermission.None) =>
        new(name, description, group, overridable, permission, DefaultIsEnabled: true, DefaultRequiresApproval: false);

    private static AgentToolDefinition Destructive(
        string name,
        string description,
        AgentToolGroup group,
        bool overridable,
        AgentToolPermission permission = AgentToolPermission.None) =>
        new(name, description, group, overridable, permission, DefaultIsEnabled: true, DefaultRequiresApproval: true);
}
