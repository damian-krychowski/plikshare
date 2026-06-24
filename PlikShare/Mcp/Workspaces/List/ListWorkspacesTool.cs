using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using PlikShare.Agents.Middleware;
using PlikShare.Agents.Operations;
using PlikShare.Agents.Tools;
using PlikShare.Core.Clock;

namespace PlikShare.Mcp.Workspaces.List;

[McpServerToolType]
public class ListWorkspacesTool
{
    [McpServerTool(Name = AgentToolNames.ListWorkspaces)]
    [Description("Lists the workspaces this agent can access, with their external ids, names and current " +
                 "size in bytes. Use a returned workspaceExternalId as input to other tools such as " +
                 "create_folder. Workspaces and boxes are separate access surfaces; you may also have direct " +
                 "access to individual boxes - call " + AgentToolNames.ListBoxes + " to discover them. If this " +
                 "tool requires approval the call returns status 'waits_for_approval' " +
                 "with an approvalRequestId - poll check_approvals and, once approved, call execute_operation " +
                 "to run it.")]
    public static async Task<AgentToolResponse> Execute(
        IHttpContextAccessor httpContextAccessor,
        ListWorkspacesAgentOperation listWorkspacesOperation,
        AgentOperationLedger operationLedger,
        AgentOperationsOptions operationsOptions,
        IClock clock,
        CancellationToken cancellationToken = default)
    {
        var httpContext = httpContextAccessor.HttpContext!;
        var agent = await httpContext.GetAgentContext();

        var definition = AgentToolCatalog.TryGet(AgentToolNames.ListWorkspaces)!;

        var effective = AgentToolCatalog.Resolve(
            agent,
            definition);

        if (!effective.IsUsable)
            throw new McpException("The list_workspaces tool is not enabled for this agent.");

        if (effective.RequiresApproval)
        {
            var expiresAt = clock.UtcNow.AddHours(
                operationsOptions.ApprovalWindowHours);

            var operationId = await operationLedger.CreatePending(
                agentId: agent.Id,
                workspaceId: null,
                toolName: AgentToolNames.ListWorkspaces,
                paramsJson: "{}",
                expiresAt: expiresAt,
                cancellationToken: cancellationToken);

            return AgentToolResponse.WaitsForApproval(
                approvalRequestId: operationId.Value,
                expiresAt: expiresAt);
        }

        var result = await listWorkspacesOperation.Execute(
            httpContext,
            cancellationToken);

        return AgentToolResponse.Executed(
            result);
    }
}
