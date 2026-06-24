using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using PlikShare.Agents.Middleware;
using PlikShare.Agents.Operations;
using PlikShare.Agents.Tools;
using PlikShare.Core.Clock;
using PlikShare.Core.Utils;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.Id;

namespace PlikShare.Mcp.BoxLinks.Delete;

[McpServerToolType]
public class DeleteBoxLinkTool
{
    [McpServerTool(Name = AgentToolNames.DeleteBoxLink)]
    [Description("Deletes a box link in a workspace the agent can access, immediately invalidating its URL. " +
                 "The box and its content stay intact. Use list_box_links to find a link's external id. If " +
                 "this tool requires approval the call returns status 'waits_for_approval' with an " +
                 "approvalRequestId - poll check_approvals and, once approved, call execute_operation to run it.")]
    public static async Task<AgentToolResponse> Execute(
        IHttpContextAccessor httpContextAccessor,
        WorkspaceAgentMembershipCache workspaceAgentMembershipCache,
        AgentWorkspaceToolOverrideReader workspaceToolOverrideReader,
        DeleteBoxLinkAgentOperation deleteBoxLinkOperation,
        AgentOperationLedger operationLedger,
        AgentOperationsOptions operationsOptions,
        IClock clock,
        [Description("External id of the workspace.")]
        string workspaceExternalId,
        [Description("External id of the box link to delete.")]
        string boxLinkExternalId,
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

        var parameters = new DeleteBoxLinkParams
        {
            WorkspaceExternalId = workspaceExternalId,
            BoxLinkExternalId = boxLinkExternalId
        };

        var definition = AgentToolCatalog.TryGet(AgentToolNames.DeleteBoxLink)!;

        var workspaceOverride = workspaceToolOverrideReader.TryGet(
            agentId: agent.Id,
            workspaceId: workspace.Id,
            toolName: AgentToolNames.DeleteBoxLink);

        var effective = AgentToolCatalog.Resolve(
            agent,
            definition,
            workspaceOverride);

        if (!effective.IsUsable)
            throw new McpException("The delete_box_link tool is not enabled for this workspace.");

        if (effective.RequiresApproval)
        {
            var expiresAt = clock.UtcNow.AddHours(
                operationsOptions.ApprovalWindowHours);

            var operationId = await operationLedger.CreatePending(
                agentId: agent.Id,
                workspaceId: workspace.Id,
                toolName: AgentToolNames.DeleteBoxLink,
                paramsJson: Json.Serialize(parameters),
                expiresAt: expiresAt,
                cancellationToken: cancellationToken);

            return AgentToolResponse.WaitsForApproval(
                approvalRequestId: operationId.Value,
                expiresAt: expiresAt);
        }

        var result = await deleteBoxLinkOperation.Execute(
            httpContext,
            parameters,
            cancellationToken);

        return AgentToolResponse.Executed(
            result);
    }
}
