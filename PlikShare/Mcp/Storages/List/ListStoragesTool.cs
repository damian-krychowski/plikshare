using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using PlikShare.Agents.Middleware;
using PlikShare.Agents.Operations;
using PlikShare.Agents.Tools;
using PlikShare.Core.Clock;

namespace PlikShare.Mcp.Storages.List;

[McpServerToolType]
public class ListStoragesTool
{
    [McpServerTool(Name = AgentToolNames.ListStorages)]
    [Description("Lists the storages this agent can use to create workspaces, with their external ids, names " +
                 "and encryption types. Pass a returned storageExternalId to create_workspace. Storages with " +
                 "full client-side encryption are omitted because agents cannot use them. If this tool requires " +
                 "approval the call returns status 'waits_for_approval' with an approvalRequestId - poll " +
                 "check_approvals and, once approved, call execute_operation to run it.")]
    public static async Task<AgentToolResponse> Execute(
        IHttpContextAccessor httpContextAccessor,
        ListStoragesAgentOperation listStoragesOperation,
        AgentOperationLedger operationLedger,
        AgentOperationsOptions operationsOptions,
        IClock clock,
        CancellationToken cancellationToken = default)
    {
        var httpContext = httpContextAccessor.HttpContext!;
        var agent = await httpContext.GetAgentContext();

        var definition = AgentToolCatalog.TryGet(AgentToolNames.ListStorages)!;

        var effective = AgentToolCatalog.Resolve(
            agent,
            definition);

        if (!effective.IsUsable)
            throw new McpException("The list_storages tool is not enabled for this agent.");

        if (effective.RequiresApproval)
        {
            var expiresAt = clock.UtcNow.AddHours(
                operationsOptions.ApprovalWindowHours);

            var operationId = await operationLedger.CreatePending(
                agentId: agent.Id,
                workspaceId: null,
                toolName: AgentToolNames.ListStorages,
                paramsJson: "{}",
                expiresAt: expiresAt,
                cancellationToken: cancellationToken);

            return AgentToolResponse.WaitsForApproval(
                approvalRequestId: operationId.Value,
                expiresAt: expiresAt);
        }

        var result = await listStoragesOperation.Execute(
            httpContext,
            cancellationToken);

        return AgentToolResponse.Executed(
            result);
    }
}
