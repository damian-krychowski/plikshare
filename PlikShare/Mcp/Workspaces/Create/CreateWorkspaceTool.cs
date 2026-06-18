using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using PlikShare.Agents.Middleware;
using PlikShare.Agents.Operations;
using PlikShare.Agents.Tools;
using PlikShare.Core.Clock;
using PlikShare.Core.Utils;

namespace PlikShare.Mcp.Workspaces.Create;

[McpServerToolType]
public class CreateWorkspaceTool
{
    [McpServerTool(Name = AgentToolNames.CreateWorkspace)]
    [Description("Creates a new workspace owned by this agent, on the given storage. The agent must have " +
                 "the 'add workspace' permission and access to the storage. Use list_storages to discover " +
                 "the storages the agent can use. Storages with full client-side encryption are not " +
                 "supported. Returns the new workspace's external id. If this tool requires approval the " +
                 "call returns status 'waits_for_approval' with an approvalRequestId — poll check_approvals " +
                 "and, once approved, call execute_operation to run it.")]
    public static async Task<AgentToolResponse> Execute(
        IHttpContextAccessor httpContextAccessor,
        CreateWorkspaceAgentOperation createWorkspaceOperation,
        AgentOperationLedger operationLedger,
        AgentOperationsOptions operationsOptions,
        IClock clock,
        [Description("Name for the new workspace.")]
        string name,
        [Description("External id of the storage to create the workspace on. Use list_storages to find it.")]
        string storageExternalId,
        CancellationToken cancellationToken = default)
    {
        var httpContext = httpContextAccessor.HttpContext!;
        var agent = await httpContext.GetAgentContext();

        var parameters = new CreateWorkspaceParams
        {
            Name = name,
            StorageExternalId = storageExternalId
        };

        var definition = AgentToolCatalog.TryGet(AgentToolNames.CreateWorkspace)!;

        // create_workspace is an instance-level tool with no workspace argument, so there is no
        // per-workspace override — availability comes from the 'add workspace' permission.
        var effective = AgentToolCatalog.Resolve(
            agent,
            definition);

        if (!effective.IsUsable)
            throw new McpException("The create_workspace tool is not enabled for this agent.");

        if (effective.RequiresApproval)
        {
            var expiresAt = clock.UtcNow.AddHours(
                operationsOptions.ApprovalWindowHours);

            var operationId = await operationLedger.CreatePending(
                agentId: agent.Id,
                workspaceId: null,
                toolName: AgentToolNames.CreateWorkspace,
                paramsJson: Json.Serialize(parameters),
                expiresAt: expiresAt,
                cancellationToken: cancellationToken);

            return AgentToolResponse.WaitsForApproval(
                approvalRequestId: operationId.Value,
                expiresAt: expiresAt);
        }

        var result = await createWorkspaceOperation.Execute(
            httpContext,
            parameters,
            cancellationToken);

        return AgentToolResponse.Executed(
            result);
    }
}
