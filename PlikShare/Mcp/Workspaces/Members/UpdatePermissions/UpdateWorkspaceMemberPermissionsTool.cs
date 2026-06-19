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

namespace PlikShare.Mcp.Workspaces.Members.UpdatePermissions;

[McpServerToolType]
public class UpdateWorkspaceMemberPermissionsTool
{
    [McpServerTool(Name = AgentToolNames.UpdateWorkspaceMemberPermissions)]
    [Description("Updates a workspace member's permissions. Set allowShare to control whether the member " +
                 "may invite further members and manage permissions. Use list_workspace_members to find a " +
                 "member's external id. If this tool requires approval the call returns status " +
                 "'waits_for_approval' with an approvalRequestId — poll check_approvals and, once approved, " +
                 "call execute_operation to run it.")]
    public static async Task<AgentToolResponse> Execute(
        IHttpContextAccessor httpContextAccessor,
        WorkspaceAgentMembershipCache workspaceAgentMembershipCache,
        AgentWorkspaceToolOverrideReader workspaceToolOverrideReader,
        UpdateWorkspaceMemberPermissionsAgentOperation updateWorkspaceMemberPermissionsOperation,
        AgentOperationLedger operationLedger,
        AgentOperationsOptions operationsOptions,
        IClock clock,
        [Description("External id of the workspace.")]
        string workspaceExternalId,
        [Description("External id of the member whose permissions to update.")]
        string memberExternalId,
        [Description("Whether the member may invite further members and manage permissions.")]
        bool allowShare,
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

        var parameters = new UpdateWorkspaceMemberPermissionsParams
        {
            WorkspaceExternalId = workspaceExternalId,
            MemberExternalId = memberExternalId,
            AllowShare = allowShare
        };

        var definition = AgentToolCatalog.TryGet(AgentToolNames.UpdateWorkspaceMemberPermissions)!;

        var workspaceOverride = workspaceToolOverrideReader.TryGet(
            agentId: agent.Id,
            workspaceId: workspace.Id,
            toolName: AgentToolNames.UpdateWorkspaceMemberPermissions);

        var effective = AgentToolCatalog.Resolve(
            agent,
            definition,
            workspaceOverride);

        if (!effective.IsUsable)
            throw new McpException("The update_workspace_member_permissions tool is not enabled for this workspace.");

        if (effective.RequiresApproval)
        {
            var expiresAt = clock.UtcNow.AddHours(
                operationsOptions.ApprovalWindowHours);

            var operationId = await operationLedger.CreatePending(
                agentId: agent.Id,
                workspaceId: workspace.Id,
                toolName: AgentToolNames.UpdateWorkspaceMemberPermissions,
                paramsJson: Json.Serialize(parameters),
                expiresAt: expiresAt,
                cancellationToken: cancellationToken);

            return AgentToolResponse.WaitsForApproval(
                approvalRequestId: operationId.Value,
                expiresAt: expiresAt);
        }

        var result = await updateWorkspaceMemberPermissionsOperation.Execute(
            httpContext,
            parameters,
            cancellationToken);

        return AgentToolResponse.Executed(
            result);
    }
}
