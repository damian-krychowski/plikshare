using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using PlikShare.Agents.Middleware;
using PlikShare.Agents.Operations;
using PlikShare.Agents.Tools;
using PlikShare.Core.Clock;
using PlikShare.Core.Utils;
using PlikShare.QuickShares.Cache;
using PlikShare.QuickShares.Id;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.Id;

namespace PlikShare.Mcp.ShareLinks.Delete;

[McpServerToolType]
public class DeleteShareLinkTool
{
    [McpServerTool(Name = AgentToolNames.DeleteShareLink)]
    [Description("Deletes a share link in a workspace the agent can access. The public URL stops working " +
                 "immediately. The shared files and folders themselves are not deleted. If this tool requires " +
                 "approval the call returns status 'waits_for_approval' with an approvalRequestId - poll " +
                 "check_approvals and, once approved, call execute_operation to run it.")]
    public static async Task<AgentToolResponse> Execute(
        IHttpContextAccessor httpContextAccessor,
        WorkspaceAgentMembershipCache workspaceAgentMembershipCache,
        AgentWorkspaceToolOverrideReader workspaceToolOverrideReader,
        QuickShareCache quickShareCache,
        DeleteShareLinkAgentOperation deleteShareLinkOperation,
        AgentOperationLedger operationLedger,
        AgentOperationsOptions operationsOptions,
        IClock clock,
        [Description("External id of the workspace.")]
        string workspaceExternalId,
        [Description("External id of the share link to delete.")]
        string shareLinkExternalId,
        CancellationToken cancellationToken = default)
    {
        var httpContext = httpContextAccessor.HttpContext!;
        var agent = await httpContext.GetAgentContext();

        var membership = await httpContext.GetWorkspaceAgentMembershipDetails(
            workspaceAgentMembershipCache,
            WorkspaceExtId.Parse(workspaceExternalId),
            cancellationToken);

        var workspace = membership.Workspace;
        workspace.ThrowIfFullyEncrypted();

        // Resolve the share link up front so we never queue an approval for one that does not exist.
        var quickShare = await quickShareCache.TryGetQuickShare(
            new QuickShareExtId(shareLinkExternalId),
            cancellationToken);

        if (quickShare is null || quickShare.Workspace.Id != workspace.Id)
            throw new McpException(
                $"Share link '{shareLinkExternalId}' was not found in workspace '{workspaceExternalId}'.");

        var parameters = new DeleteShareLinkParams
        {
            WorkspaceExternalId = workspaceExternalId,
            ShareLinkExternalId = shareLinkExternalId
        };

        // Cascade the agent's config for this workspace: per-workspace override → global → catalog
        // default. The workspace override governs both whether the tool is usable here and whether
        // it needs approval — evaluating without it would ignore the admin's per-workspace settings.
        var definition = AgentToolCatalog.TryGet(AgentToolNames.DeleteShareLink)!;

        var workspaceOverride = workspaceToolOverrideReader.TryGet(
            agentId: agent.Id,
            workspaceId: workspace.Id,
            toolName: AgentToolNames.DeleteShareLink);

        var effective = AgentToolCatalog.Resolve(
            agent,
            definition,
            workspaceOverride);

        if (!effective.IsUsable)
            throw new McpException("The delete_share_link tool is not enabled for this workspace.");

        if (effective.RequiresApproval)
        {
            var expiresAt = clock.UtcNow.AddHours(
                operationsOptions.ApprovalWindowHours);

            var operationId = await operationLedger.CreatePending(
                agentId: agent.Id,
                workspaceId: workspace.Id,
                toolName: AgentToolNames.DeleteShareLink,
                paramsJson: Json.Serialize(parameters),
                expiresAt: expiresAt,
                cancellationToken: cancellationToken);

            return AgentToolResponse.WaitsForApproval(
                approvalRequestId: operationId.Value,
                expiresAt: expiresAt);
        }

        var result = await deleteShareLinkOperation.Execute(
            httpContext,
            parameters,
            cancellationToken);

        return AgentToolResponse.Executed(
            result);
    }
}
