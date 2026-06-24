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

namespace PlikShare.Mcp.Boxes.Members.UpdatePermissions;

[McpServerToolType]
public class UpdateBoxMemberPermissionsTool
{
    [McpServerTool(Name = AgentToolNames.UpdateBoxMemberPermissions)]
    [Description("Updates a box member's permissions in a workspace the agent can access. Provide any of the " +
                 "permission flags (allowDownload, allowUpload, allowList, allowDeleteFile, allowRenameFile, " +
                 "allowMoveItems, allowCreateFolder, allowRenameFolder, allowDeleteFolder); any flag you omit " +
                 "keeps its current value. At least one flag must be given. Use list_box_members to find a " +
                 "member's external id. If this tool requires approval the call returns status " +
                 "'waits_for_approval' with an approvalRequestId - poll check_approvals and, once approved, " +
                 "call execute_operation to run it.")]
    public static async Task<AgentToolResponse> Execute(
        IHttpContextAccessor httpContextAccessor,
        WorkspaceAgentMembershipCache workspaceAgentMembershipCache,
        AgentWorkspaceToolOverrideReader workspaceToolOverrideReader,
        UpdateBoxMemberPermissionsAgentOperation updateBoxMemberPermissionsOperation,
        AgentOperationLedger operationLedger,
        AgentOperationsOptions operationsOptions,
        IClock clock,
        [Description("External id of the workspace.")]
        string workspaceExternalId,
        [Description("External id of the box.")]
        string boxExternalId,
        [Description("External id of the member whose permissions to update.")]
        string memberExternalId,
        [Description("Optional: allow downloading files.")]
        bool? allowDownload = null,
        [Description("Optional: allow uploading files.")]
        bool? allowUpload = null,
        [Description("Optional: allow listing the box content.")]
        bool? allowList = null,
        [Description("Optional: allow deleting files.")]
        bool? allowDeleteFile = null,
        [Description("Optional: allow renaming files.")]
        bool? allowRenameFile = null,
        [Description("Optional: allow moving items.")]
        bool? allowMoveItems = null,
        [Description("Optional: allow creating folders.")]
        bool? allowCreateFolder = null,
        [Description("Optional: allow renaming folders.")]
        bool? allowRenameFolder = null,
        [Description("Optional: allow deleting folders.")]
        bool? allowDeleteFolder = null,
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

        var parameters = new UpdateBoxMemberPermissionsParams
        {
            WorkspaceExternalId = workspaceExternalId,
            BoxExternalId = boxExternalId,
            MemberExternalId = memberExternalId,
            AllowDownload = allowDownload,
            AllowUpload = allowUpload,
            AllowList = allowList,
            AllowDeleteFile = allowDeleteFile,
            AllowRenameFile = allowRenameFile,
            AllowMoveItems = allowMoveItems,
            AllowCreateFolder = allowCreateFolder,
            AllowRenameFolder = allowRenameFolder,
            AllowDeleteFolder = allowDeleteFolder
        };

        if (!parameters.HasPermissionChange)
            throw new McpException("Provide at least one permission flag to update.");

        var definition = AgentToolCatalog.TryGet(AgentToolNames.UpdateBoxMemberPermissions)!;

        var workspaceOverride = workspaceToolOverrideReader.TryGet(
            agentId: agent.Id,
            workspaceId: workspace.Id,
            toolName: AgentToolNames.UpdateBoxMemberPermissions);

        var effective = AgentToolCatalog.Resolve(
            agent,
            definition,
            workspaceOverride);

        if (!effective.IsUsable)
            throw new McpException("The update_box_member_permissions tool is not enabled for this workspace.");

        if (effective.RequiresApproval)
        {
            var expiresAt = clock.UtcNow.AddHours(
                operationsOptions.ApprovalWindowHours);

            var operationId = await operationLedger.CreatePending(
                agentId: agent.Id,
                workspaceId: workspace.Id,
                toolName: AgentToolNames.UpdateBoxMemberPermissions,
                paramsJson: Json.Serialize(parameters),
                expiresAt: expiresAt,
                cancellationToken: cancellationToken);

            return AgentToolResponse.WaitsForApproval(
                approvalRequestId: operationId.Value,
                expiresAt: expiresAt);
        }

        var result = await updateBoxMemberPermissionsOperation.Execute(
            httpContext,
            parameters,
            cancellationToken);

        return AgentToolResponse.Executed(
            result);
    }
}
