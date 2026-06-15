using ModelContextProtocol;
using PlikShare.Agents.Middleware;
using PlikShare.Storages.Encryption;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.Id;

namespace PlikShare.Mcp;

public static class McpAgentContextExtensions
{
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

    public static void ThrowIfFullyEncrypted(this WorkspaceContext workspace)
    {
        if (workspace.EncryptionType == StorageEncryptionType.Full)
            throw new McpException(
                $"Workspace '{workspace.ExternalId}' uses full client-side encryption " +
                "and cannot be accessed by agents.");
    }
}
