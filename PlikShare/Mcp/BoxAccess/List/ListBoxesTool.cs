using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using PlikShare.Agents.Middleware;
using PlikShare.Agents.Operations;
using PlikShare.Agents.Tools;
using PlikShare.Core.Clock;

namespace PlikShare.Mcp.BoxAccess.List;

[McpServerToolType]
public class ListBoxesTool
{
    [McpServerTool(Name = AgentToolNames.ListBoxes)]
    [Description("Lists the boxes shared directly with this agent, with their external ids, names, enabled " +
                 "state and the workspace each belongs to. A box is a curated view of one folder; use a " +
                 "returned boxExternalId with the other box tools (get_box_details, list_box_content, " +
                 "read_box_file, …) to work inside it. These boxes are separate from the workspaces you may be " +
                 "a member of - call " + AgentToolNames.ListWorkspaces + " for those. If this tool requires " +
                 "approval the call returns status " +
                 "'waits_for_approval' with an approvalRequestId - poll check_approvals and, once approved, " +
                 "call execute_operation to run it.")]
    public static async Task<AgentToolResponse> Execute(
        IHttpContextAccessor httpContextAccessor,
        ListBoxesAgentOperation listBoxesOperation,
        AgentOperationLedger operationLedger,
        AgentOperationsOptions operationsOptions,
        IClock clock,
        CancellationToken cancellationToken = default)
    {
        var httpContext = httpContextAccessor.HttpContext!;
        var agent = await httpContext.GetAgentContext();

        var definition = AgentToolCatalog.TryGet(AgentToolNames.ListBoxes)!;

        var effective = AgentToolCatalog.Resolve(
            agent,
            definition);

        if (!effective.IsUsable)
            throw new McpException("The list_boxes tool is not enabled for this agent.");

        if (effective.RequiresApproval)
        {
            var expiresAt = clock.UtcNow.AddHours(
                operationsOptions.ApprovalWindowHours);

            var operationId = await operationLedger.CreatePending(
                agentId: agent.Id,
                workspaceId: null,
                toolName: AgentToolNames.ListBoxes,
                paramsJson: "{}",
                expiresAt: expiresAt,
                cancellationToken: cancellationToken);

            return AgentToolResponse.WaitsForApproval(
                approvalRequestId: operationId.Value,
                expiresAt: expiresAt);
        }

        var result = await listBoxesOperation.Execute(
            httpContext,
            cancellationToken);

        return AgentToolResponse.Executed(
            result);
    }
}
