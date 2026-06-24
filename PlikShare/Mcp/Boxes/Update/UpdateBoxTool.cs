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

namespace PlikShare.Mcp.Boxes.Update;

[McpServerToolType]
public class UpdateBoxTool
{
    [McpServerTool(Name = AgentToolNames.UpdateBox)]
    [Description("Updates a box in a workspace the agent can access. Provide any combination of: name (to " +
                 "rename it), isEnabled (to enable or disable public/member access), folderExternalId (to " +
                 "point the box at a different folder). At least one must be given. If this tool requires " +
                 "approval the call returns status 'waits_for_approval' with an approvalRequestId - poll " +
                 "check_approvals and, once approved, call execute_operation to run it.")]
    public static async Task<AgentToolResponse> Execute(
        IHttpContextAccessor httpContextAccessor,
        WorkspaceAgentMembershipCache workspaceAgentMembershipCache,
        AgentWorkspaceToolOverrideReader workspaceToolOverrideReader,
        UpdateBoxAgentOperation updateBoxOperation,
        AgentOperationLedger operationLedger,
        AgentOperationsOptions operationsOptions,
        IClock clock,
        [Description("External id of the workspace.")]
        string workspaceExternalId,
        [Description("External id of the box to update.")]
        string boxExternalId,
        [Description("Optional new name for the box.")]
        string? name = null,
        [Description("Optional new enabled state for the box.")]
        bool? isEnabled = null,
        [Description("Optional external id of a folder to point the box at.")]
        string? folderExternalId = null,
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

        var trimmedName = name?.Trim();

        if (name is not null && string.IsNullOrWhiteSpace(trimmedName))
            throw new McpException("The box name cannot be empty.");

        if (trimmedName is null && isEnabled is null && string.IsNullOrWhiteSpace(folderExternalId))
            throw new McpException("Provide at least one of name, isEnabled or folderExternalId to update.");

        var parameters = new UpdateBoxParams
        {
            WorkspaceExternalId = workspaceExternalId,
            BoxExternalId = boxExternalId,
            Name = trimmedName,
            IsEnabled = isEnabled,
            FolderExternalId = string.IsNullOrWhiteSpace(folderExternalId) ? null : folderExternalId
        };

        var definition = AgentToolCatalog.TryGet(AgentToolNames.UpdateBox)!;

        var workspaceOverride = workspaceToolOverrideReader.TryGet(
            agentId: agent.Id,
            workspaceId: workspace.Id,
            toolName: AgentToolNames.UpdateBox);

        var effective = AgentToolCatalog.Resolve(
            agent,
            definition,
            workspaceOverride);

        if (!effective.IsUsable)
            throw new McpException("The update_box tool is not enabled for this workspace.");

        if (effective.RequiresApproval)
        {
            var expiresAt = clock.UtcNow.AddHours(
                operationsOptions.ApprovalWindowHours);

            var operationId = await operationLedger.CreatePending(
                agentId: agent.Id,
                workspaceId: workspace.Id,
                toolName: AgentToolNames.UpdateBox,
                paramsJson: Json.Serialize(parameters),
                expiresAt: expiresAt,
                cancellationToken: cancellationToken);

            return AgentToolResponse.WaitsForApproval(
                approvalRequestId: operationId.Value,
                expiresAt: expiresAt);
        }

        var result = await updateBoxOperation.Execute(
            httpContext,
            parameters,
            cancellationToken);

        return AgentToolResponse.Executed(
            result);
    }
}
