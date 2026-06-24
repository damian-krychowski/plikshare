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

namespace PlikShare.Mcp.Workspaces.Rename;

[McpServerToolType]
public class RenameWorkspaceTool
{
    [McpServerTool(Name = AgentToolNames.RenameWorkspace)]
    [Description("Renames a workspace the agent has access to. If this tool requires approval the call " +
                 "returns status 'waits_for_approval' with an approvalRequestId - poll check_approvals and, " +
                 "once approved, call execute_operation to run it.")]
    public static async Task<AgentToolResponse> Execute(
        IHttpContextAccessor httpContextAccessor,
        WorkspaceAgentMembershipCache workspaceAgentMembershipCache,
        AgentWorkspaceToolOverrideReader workspaceToolOverrideReader,
        RenameWorkspaceAgentOperation renameWorkspaceOperation,
        AgentOperationLedger operationLedger,
        AgentOperationsOptions operationsOptions,
        IClock clock,
        [Description("External id of the workspace to rename.")]
        string workspaceExternalId,
        [Description("New name for the workspace.")]
        string name,
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

        var parameters = new RenameWorkspaceParams
        {
            WorkspaceExternalId = workspaceExternalId,
            Name = name
        };

        var definition = AgentToolCatalog.TryGet(AgentToolNames.RenameWorkspace)!;

        var workspaceOverride = workspaceToolOverrideReader.TryGet(
            agentId: agent.Id,
            workspaceId: workspace.Id,
            toolName: AgentToolNames.RenameWorkspace);

        var effective = AgentToolCatalog.Resolve(
            agent,
            definition,
            workspaceOverride);

        if (!effective.IsUsable)
            throw new McpException("The rename_workspace tool is not enabled for this workspace.");

        if (effective.RequiresApproval)
        {
            var expiresAt = clock.UtcNow.AddHours(
                operationsOptions.ApprovalWindowHours);

            var operationId = await operationLedger.CreatePending(
                agentId: agent.Id,
                workspaceId: workspace.Id,
                toolName: AgentToolNames.RenameWorkspace,
                paramsJson: Json.Serialize(parameters),
                expiresAt: expiresAt,
                cancellationToken: cancellationToken);

            return AgentToolResponse.WaitsForApproval(
                approvalRequestId: operationId.Value,
                expiresAt: expiresAt);
        }

        var result = await renameWorkspaceOperation.Execute(
            httpContext,
            parameters,
            cancellationToken);

        return AgentToolResponse.Executed(
            result);
    }
}
