using PlikShare.Agents.Cache;

namespace PlikShare.Agents.Tools;

/// <summary>
/// How a tool is grouped in the configuration UI. <see cref="Instance"/> tools act at the
/// instance level (listing/creating workspaces, listing boxes shared with the agent); <see cref="Workspace"/>
/// tools act on the content of workspaces; <see cref="Box"/> tools act inside a single box the agent was
/// granted direct access to. This is a display concern only — whether a tool can be overridden per workspace
/// or per box is a separate flag (<see cref="AgentToolDefinition.IsWorkspaceOverridable"/> /
/// <see cref="AgentToolDefinition.IsBoxOverridable"/>).
/// </summary>
public enum AgentToolGroup
{
    Instance = 0,
    Workspace = 1,
    Box = 2
}

/// <summary>
/// What a tool does, used purely to surface intent in the configuration UI (e.g. a "destructive" pill).
/// It does not drive behaviour — the enabled/approval config does that — but it lets operators see at a
/// glance which tools delete data (<see cref="Destructive"/>) or grant people access (<see cref="Invite"/>).
/// </summary>
public enum AgentToolKind
{
    Read = 0,
    Write = 1,
    Destructive = 2,
    Invite = 3
}

/// <summary>
/// Canonical definition of an agent tool: the single source of truth for its UI group, whether it
/// can carry a per-workspace override, and the per-agent defaults used when no explicit config row
/// exists. An agent has no admin-console permissions — every capability is expressed here, by the
/// tool's enabled/approval config alone.
/// </summary>
public sealed record AgentToolDefinition(
    string Name,
    string Description,
    AgentToolGroup Group,
    bool IsWorkspaceOverridable,
    bool DefaultIsEnabled,
    bool DefaultRequiresApproval,
    AgentToolKind Kind)
{
    /// <summary>
    /// A box-scoped tool carries a per-box override (and only a per-box one) — these are the tools an agent
    /// uses inside a box it was granted direct access to. Workspace and instance tools are never box-overridable.
    /// </summary>
    public bool IsBoxOverridable => Group == AgentToolGroup.Box;
}

/// <summary>
/// A partial override at a single cascade level (workspace or box). A null dimension means "inherit
/// this dimension from the next level down"; each dimension cascades independently.
/// </summary>
public sealed record AgentToolScopeOverride(
    bool? IsEnabled,
    bool? RequiresApproval);

public sealed record EffectiveAgentToolConfig(
    bool IsEnabled,
    bool RequiresApproval)
{
    public bool IsUsable => IsEnabled;
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
        // create_workspace is the agent's most privileged instance-level action, so it stays off until
        // an operator explicitly enables it for the agent.
        Write(AgentToolNames.CreateWorkspace, "Creates a new workspace owned by the agent.", AgentToolGroup.Instance, overridable: false, enabledByDefault: false),

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
        Destructive(AgentToolNames.DeleteShareLink, "Deletes a share link. The shared files and folders stay intact.", AgentToolGroup.Workspace, overridable: true),

        Read(AgentToolNames.ListWorkspaceMembers, "Lists the members of a workspace.", AgentToolGroup.Workspace, overridable: true),
        // Inviting people grants humans access and sends emails, so it requires approval by default.
        Invite(AgentToolNames.InviteWorkspaceMembers, "Invites people by email to a workspace.", AgentToolGroup.Workspace, overridable: true),
        Write(AgentToolNames.UpdateWorkspaceMemberPermissions, "Updates a workspace member's permissions.", AgentToolGroup.Workspace, overridable: true),
        Destructive(AgentToolNames.RevokeWorkspaceMember, "Removes a member from a workspace.", AgentToolGroup.Workspace, overridable: true),

        Read(AgentToolNames.ListWorkspaceBoxes, "Lists the boxes of a workspace.", AgentToolGroup.Workspace, overridable: true),
        Read(AgentToolNames.GetBox, "Reads the details of a single box.", AgentToolGroup.Workspace, overridable: true),
        Write(AgentToolNames.CreateBox, "Creates a box in a workspace.", AgentToolGroup.Workspace, overridable: true),
        Write(AgentToolNames.UpdateBox, "Updates a box's name, enabled state or folder.", AgentToolGroup.Workspace, overridable: true),
        Destructive(AgentToolNames.DeleteBox, "Deletes a box.", AgentToolGroup.Workspace, overridable: true),

        Read(AgentToolNames.ListBoxLinks, "Lists the public links of a box.", AgentToolGroup.Workspace, overridable: true),
        Write(AgentToolNames.CreateBoxLink, "Creates a public link to a box.", AgentToolGroup.Workspace, overridable: true),
        Write(AgentToolNames.UpdateBoxLink, "Updates a box link's name, enabled state, permissions or widget origins.", AgentToolGroup.Workspace, overridable: true),
        Destructive(AgentToolNames.DeleteBoxLink, "Deletes a box link.", AgentToolGroup.Workspace, overridable: true),
        Write(AgentToolNames.RegenerateBoxLinkAccessCode, "Regenerates a box link's access code, invalidating the old URL.", AgentToolGroup.Workspace, overridable: true),

        Read(AgentToolNames.ListBoxMembers, "Lists the members of a box.", AgentToolGroup.Workspace, overridable: true),
        // Inviting people grants humans access and sends emails, so it requires approval by default.
        Invite(AgentToolNames.InviteBoxMembers, "Invites people by email to a box.", AgentToolGroup.Workspace, overridable: true),
        Write(AgentToolNames.UpdateBoxMemberPermissions, "Updates a box member's permissions.", AgentToolGroup.Workspace, overridable: true),
        Destructive(AgentToolNames.RevokeBoxMember, "Removes a member from a box.", AgentToolGroup.Workspace, overridable: true),

        // Box-access tools — the agent acts as a consumer inside a box it was granted direct access to
        // (ba_box_agents), scoped to the box's exposed folder. They carry per-box overrides only (never a
        // per-workspace one); list_boxes is the instance-level discovery entry that mirrors list_workspaces.
        Read(AgentToolNames.ListBoxes, "Lists the boxes shared directly with the agent.", AgentToolGroup.Instance, overridable: false),
        Read(AgentToolNames.GetBoxDetails, "Reads a box's details — its name, enabled state and the root folder it exposes.", AgentToolGroup.Box, overridable: false),
        Read(AgentToolNames.ListBoxContent, "Lists the folders and files inside a box, or one of its folders.", AgentToolGroup.Box, overridable: false),
        Read(AgentToolNames.ReadBoxFile, "Reads the text content of a file inside a box.", AgentToolGroup.Box, overridable: false),
        Read(AgentToolNames.GetBoxFileDownloadLink, "Creates a short-lived link to download a single file from a box.", AgentToolGroup.Box, overridable: false),
        Read(AgentToolNames.GetBoxBulkDownloadLink, "Creates a link to download several files or folders from a box as one ZIP archive.", AgentToolGroup.Box, overridable: false),
        Read(AgentToolNames.SearchBox, "Searches for files and folders inside a box.", AgentToolGroup.Box, overridable: false),

        Write(AgentToolNames.CreateBoxFolder, "Creates a new folder inside a box.", AgentToolGroup.Box, overridable: false),
        Write(AgentToolNames.CreateBoxFile, "Creates a new text file inside a box.", AgentToolGroup.Box, overridable: false),
        Write(AgentToolNames.RenameBoxFile, "Renames a file inside a box, keeping its extension.", AgentToolGroup.Box, overridable: false),
        Write(AgentToolNames.RenameBoxFolder, "Renames a folder inside a box.", AgentToolGroup.Box, overridable: false),
        Write(AgentToolNames.MoveBoxItems, "Moves files and folders into another folder inside a box.", AgentToolGroup.Box, overridable: false),

        Destructive(AgentToolNames.DeleteBoxItems, "Deletes files and/or folders inside a box, including whole folder trees.", AgentToolGroup.Box, overridable: false)
    ];

    private static readonly IReadOnlyDictionary<string, AgentToolDefinition> ByName =
        All.ToDictionary(tool => tool.Name);

    public static IReadOnlyCollection<string> Names { get; } =
        All.Select(tool => tool.Name).ToHashSet();

    public static AgentToolDefinition? TryGet(string name) =>
        ByName.GetValueOrDefault(name);

    /// <summary>
    /// Resolves the effective config for a tool invocation by cascading, per dimension,
    /// box override → workspace override → agent global → catalog default. The box override is the
    /// finest scope; a tool that operates inside a box passes it, everything else leaves it null.
    /// </summary>
    public static EffectiveAgentToolConfig Resolve(
        AgentContext agent,
        AgentToolDefinition definition,
        AgentToolScopeOverride? workspaceOverride = null,
        AgentToolScopeOverride? boxOverride = null)
    {
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
            IsEnabled: isEnabled,
            RequiresApproval: requiresApproval);
    }

    private static AgentToolDefinition Read(
        string name,
        string description,
        AgentToolGroup group,
        bool overridable,
        bool enabledByDefault = true) =>
        new(name, description, group, overridable, DefaultIsEnabled: enabledByDefault, DefaultRequiresApproval: false, Kind: AgentToolKind.Read);

    private static AgentToolDefinition Write(
        string name,
        string description,
        AgentToolGroup group,
        bool overridable,
        bool enabledByDefault = true) =>
        new(name, description, group, overridable, DefaultIsEnabled: enabledByDefault, DefaultRequiresApproval: false, Kind: AgentToolKind.Write);

    private static AgentToolDefinition Destructive(
        string name,
        string description,
        AgentToolGroup group,
        bool overridable,
        bool enabledByDefault = true) =>
        new(name, description, group, overridable, DefaultIsEnabled: enabledByDefault, DefaultRequiresApproval: true, Kind: AgentToolKind.Destructive);

    // Inviting people is an outward-facing action — it grants humans access and sends email — so it
    // mirrors Destructive in requiring approval by default, but reads as its own intent.
    private static AgentToolDefinition Invite(
        string name,
        string description,
        AgentToolGroup group,
        bool overridable,
        bool enabledByDefault = true) =>
        new(name, description, group, overridable, DefaultIsEnabled: enabledByDefault, DefaultRequiresApproval: true, Kind: AgentToolKind.Invite);
}
