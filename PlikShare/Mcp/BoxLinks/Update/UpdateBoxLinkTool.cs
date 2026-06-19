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

namespace PlikShare.Mcp.BoxLinks.Update;

[McpServerToolType]
public class UpdateBoxLinkTool
{
    [McpServerTool(Name = AgentToolNames.UpdateBoxLink)]
    [Description("Updates a box link in a workspace the agent can access. Provide any combination of: name, " +
                 "isEnabled, the permission flags (allowDownload, allowUpload, allowList, allowDeleteFile, " +
                 "allowRenameFile, allowMoveItems, allowCreateFolder, allowRenameFolder, allowDeleteFolder), " +
                 "and widgetOrigins (the list of domains allowed to embed the link; pass an empty list to " +
                 "clear). Any permission flag you omit keeps its current value. At least one field must be " +
                 "given. If this tool requires approval the call returns status 'waits_for_approval' with an " +
                 "approvalRequestId — poll check_approvals and, once approved, call execute_operation to run it.")]
    public static async Task<AgentToolResponse> Execute(
        IHttpContextAccessor httpContextAccessor,
        WorkspaceAgentMembershipCache workspaceAgentMembershipCache,
        AgentWorkspaceToolOverrideReader workspaceToolOverrideReader,
        UpdateBoxLinkAgentOperation updateBoxLinkOperation,
        AgentOperationLedger operationLedger,
        AgentOperationsOptions operationsOptions,
        IClock clock,
        [Description("External id of the workspace.")]
        string workspaceExternalId,
        [Description("External id of the box link to update.")]
        string boxLinkExternalId,
        [Description("Optional new name for the box link.")]
        string? name = null,
        [Description("Optional new enabled state for the box link.")]
        bool? isEnabled = null,
        [Description("Optional: allow downloading files through the link.")]
        bool? allowDownload = null,
        [Description("Optional: allow uploading files through the link.")]
        bool? allowUpload = null,
        [Description("Optional: allow listing the box content through the link.")]
        bool? allowList = null,
        [Description("Optional: allow deleting files through the link.")]
        bool? allowDeleteFile = null,
        [Description("Optional: allow renaming files through the link.")]
        bool? allowRenameFile = null,
        [Description("Optional: allow moving items through the link.")]
        bool? allowMoveItems = null,
        [Description("Optional: allow creating folders through the link.")]
        bool? allowCreateFolder = null,
        [Description("Optional: allow renaming folders through the link.")]
        bool? allowRenameFolder = null,
        [Description("Optional: allow deleting folders through the link.")]
        bool? allowDeleteFolder = null,
        [Description("Optional list of domains allowed to embed the link as a widget. Pass an empty list to clear.")]
        string[]? widgetOrigins = null,
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
            throw new McpException("The box link name cannot be empty.");

        var parameters = new UpdateBoxLinkParams
        {
            WorkspaceExternalId = workspaceExternalId,
            BoxLinkExternalId = boxLinkExternalId,
            Name = trimmedName,
            IsEnabled = isEnabled,
            AllowDownload = allowDownload,
            AllowUpload = allowUpload,
            AllowList = allowList,
            AllowDeleteFile = allowDeleteFile,
            AllowRenameFile = allowRenameFile,
            AllowMoveItems = allowMoveItems,
            AllowCreateFolder = allowCreateFolder,
            AllowRenameFolder = allowRenameFolder,
            AllowDeleteFolder = allowDeleteFolder,
            WidgetOrigins = widgetOrigins
        };

        if (trimmedName is null
            && isEnabled is null
            && !parameters.HasPermissionChange
            && widgetOrigins is null)
        {
            throw new McpException(
                "Provide at least one of name, isEnabled, a permission flag or widgetOrigins to update.");
        }

        var definition = AgentToolCatalog.TryGet(AgentToolNames.UpdateBoxLink)!;

        var workspaceOverride = workspaceToolOverrideReader.TryGet(
            agentId: agent.Id,
            workspaceId: workspace.Id,
            toolName: AgentToolNames.UpdateBoxLink);

        var effective = AgentToolCatalog.Resolve(
            agent,
            definition,
            workspaceOverride);

        if (!effective.IsUsable)
            throw new McpException("The update_box_link tool is not enabled for this workspace.");

        if (effective.RequiresApproval)
        {
            var expiresAt = clock.UtcNow.AddHours(
                operationsOptions.ApprovalWindowHours);

            var operationId = await operationLedger.CreatePending(
                agentId: agent.Id,
                workspaceId: workspace.Id,
                toolName: AgentToolNames.UpdateBoxLink,
                paramsJson: Json.Serialize(parameters),
                expiresAt: expiresAt,
                cancellationToken: cancellationToken);

            return AgentToolResponse.WaitsForApproval(
                approvalRequestId: operationId.Value,
                expiresAt: expiresAt);
        }

        var result = await updateBoxLinkOperation.Execute(
            httpContext,
            parameters,
            cancellationToken);

        return AgentToolResponse.Executed(
            result);
    }
}
