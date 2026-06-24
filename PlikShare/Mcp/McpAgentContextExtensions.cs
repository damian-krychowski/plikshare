using ModelContextProtocol;
using PlikShare.Agents.BoxAccess;
using PlikShare.Agents.Cache;
using PlikShare.Agents.Middleware;
using PlikShare.Boxes.Cache;
using PlikShare.Boxes.Id;
using PlikShare.Boxes.Permissions;
using PlikShare.BoxLinks.Cache;
using PlikShare.BoxLinks.Id;
using PlikShare.Core.UserIdentity;
using PlikShare.Storages.Encryption;
using PlikShare.Users.Id;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.Id;
using BoxAccessContext = PlikShare.BoxExternalAccess.Authorization.BoxAccess;

namespace PlikShare.Mcp;

public static class McpAgentContextExtensions
{
    /// <summary>
    /// Resolves a box membership the agent manages within a workspace it can access. The membership's box
    /// must belong to that workspace and not be in the process of being deleted. Throws an
    /// <see cref="McpException"/> otherwise.
    /// </summary>
    public static async Task<BoxMembershipContext> GetAgentBoxMembershipInWorkspace(
        this HttpContext httpContext,
        BoxMembershipCache boxMembershipCache,
        WorkspaceContext workspace,
        BoxExtId boxExternalId,
        UserExtId memberExternalId,
        CancellationToken cancellationToken)
    {
        var membership = await boxMembershipCache.TryGetBoxMembership(
            boxExternalId,
            memberExternalId,
            cancellationToken);

        if (membership is null || membership.Box.IsBeingDeleted || membership.Box.Workspace.Id != workspace.Id)
            throw new McpException(
                $"Member '{memberExternalId}' was not found in box '{boxExternalId}' of workspace '{workspace.ExternalId}'.");

        return membership;
    }

    /// <summary>
    /// Resolves a box link the agent manages within a workspace it can access. The box link's box must
    /// belong to that workspace and not be in the process of being deleted. Throws an
    /// <see cref="McpException"/> otherwise.
    /// </summary>
    public static async Task<BoxLinkContext> GetAgentBoxLinkInWorkspace(
        this HttpContext httpContext,
        BoxLinkCache boxLinkCache,
        WorkspaceContext workspace,
        BoxLinkExtId boxLinkExternalId,
        CancellationToken cancellationToken)
    {
        var boxLink = await boxLinkCache.TryGetBoxLink(
            boxLinkExternalId,
            cancellationToken);

        if (boxLink is null || boxLink.Box.IsBeingDeleted || boxLink.Box.Workspace.Id != workspace.Id)
            throw new McpException(
                $"Box link '{boxLinkExternalId}' was not found in workspace '{workspace.ExternalId}'.");

        return boxLink;
    }

    /// <summary>
    /// Resolves a box the agent manages within a workspace it can access. The agent's authority over a
    /// box is its workspace membership — the box must belong to that workspace and not be in the process
    /// of being deleted. Throws an <see cref="McpException"/> otherwise.
    /// </summary>
    public static async Task<BoxContext> GetAgentBoxInWorkspace(
        this HttpContext httpContext,
        BoxCache boxCache,
        WorkspaceContext workspace,
        BoxExtId boxExternalId,
        CancellationToken cancellationToken)
    {
        var box = await boxCache.TryGetBox(
            boxExternalId,
            cancellationToken);

        if (box is null || box.IsBeingDeleted || box.Workspace.Id != workspace.Id)
            throw new McpException(
                $"Box '{boxExternalId}' was not found in workspace '{workspace.ExternalId}'.");

        return box;
    }

    public static async Task<WorkspaceAgentMembershipContext> GetWorkspaceAgentMembershipDetails(
        this HttpContext httpContext,
        WorkspaceAgentMembershipCache workspaceAgentMembershipCache,
        WorkspaceExtId workspaceExternalId,
        CancellationToken cancellationToken)
    {
        var agent = await httpContext.GetAgentContext();

        var membership = await workspaceAgentMembershipCache.TryGetWorkspaceAgentMembership(
            workspaceExternalId: workspaceExternalId,
            agentExternalId: agent.ExternalId,
            cancellationToken: cancellationToken);

        if (membership is null || !membership.IsAvailableForAgent)
            throw new McpException(
                $"Workspace '{workspaceExternalId}' was not found or is not accessible to this agent.");

        return membership;
    }

    /// <summary>
    /// Resolves a box the agent was granted direct access to (a <c>ba_box_agents</c> row), as a
    /// <see cref="BoxAccess"/> the box-external-access queries already understand. The agent acts as a
    /// consumer scoped to the box's exposed folder, so its identity is an <see cref="AgentIdentity"/> and
    /// — unlike a human member — it is not constrained by per-box permission flags (the tool layer governs
    /// what it may do), hence <see cref="BoxPermissions.Full"/>. Throws an <see cref="McpException"/> when
    /// the box does not exist, is being deleted, sits in a full-encryption workspace, or is not shared with
    /// this agent. Callers that mutate or list content must additionally honour <see cref="BoxAccess.IsOff"/>.
    /// </summary>
    public static async Task<BoxAccessContext> GetAgentBoxAccess(
        this HttpContext httpContext,
        AgentContext agent,
        BoxCache boxCache,
        AgentBoxAccessCache boxAccessCache,
        BoxExtId boxExternalId,
        CancellationToken cancellationToken)
    {
        var box = await boxCache.TryGetBox(
            boxExternalId,
            cancellationToken);

        if (box is null || box.IsBeingDeleted || !await boxAccessCache.HasAccess(agent.Id, box.Id, cancellationToken))
            throw new McpException(
                $"Box '{boxExternalId}' was not found or is not accessible to this agent.");

        box.Workspace.ThrowIfFullyEncrypted();

        return new BoxAccessContext(
            IsEnabled: box.IsEnabled,
            Box: box,
            BoxLink: null,
            Permissions: BoxPermissions.Full(),
            UserIdentity: new AgentIdentity(agent.ExternalId),
            UserEmail: null,
            UserIp: httpContext.Connection.RemoteIpAddress?.ToString());
    }

    public static void ThrowIfFullyEncrypted(this WorkspaceContext workspace)
    {
        if (workspace.EncryptionType == StorageEncryptionType.Full)
            throw new McpException(
                $"Workspace '{workspace.ExternalId}' uses full client-side encryption " +
                "and cannot be accessed by agents.");
    }
}
