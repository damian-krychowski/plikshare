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

namespace PlikShare.Mcp.Files.BulkDownloadLink;

[McpServerToolType]
public class GetBulkDownloadLinkTool
{
    [McpServerTool(Name = AgentToolNames.GetBulkDownloadLink)]
    [Description("Creates a short-lived link that downloads the selected files and/or folders from a workspace " +
                 "as a single ZIP archive, to hand to a user. Folders are included with all their contents. " +
                 "Provide at least one id in fileExternalIds or folderExternalIds. Anyone with the link can " +
                 "download without logging in until it expires, so keep the expiry short. If this tool requires " +
                 "approval the call returns status 'waits_for_approval' with an approvalRequestId - poll " +
                 "check_approvals and, once approved, call execute_operation to run it.")]
    public static async Task<AgentToolResponse> Execute(
        IHttpContextAccessor httpContextAccessor,
        WorkspaceAgentMembershipCache workspaceAgentMembershipCache,
        AgentWorkspaceToolOverrideReader workspaceToolOverrideReader,
        GetBulkDownloadLinkAgentOperation getBulkDownloadLinkOperation,
        AgentOperationLedger operationLedger,
        AgentOperationsOptions operationsOptions,
        IClock clock,
        [Description("External id of the workspace.")]
        string workspaceExternalId,
        [Description("External ids of files to include. Optional if folders are given.")]
        string[]? fileExternalIds = null,
        [Description("External ids of folders to include, downloaded with all their contents. Optional if files are given.")]
        string[]? folderExternalIds = null,
        [Description("Optional external ids of files to carve out of the included folders.")]
        string[]? excludedFileExternalIds = null,
        [Description("Optional external ids of subfolders to carve out of the included folders.")]
        string[]? excludedFolderExternalIds = null,
        [Description("How long the link stays valid, in minutes. Default 15, minimum 1, maximum 1440 (24h).")]
        int? expiresInMinutes = null,
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

        var files = fileExternalIds ?? [];
        var folders = folderExternalIds ?? [];

        if (files.Length == 0 && folders.Length == 0)
            throw new McpException("Provide at least one id in fileExternalIds or folderExternalIds.");

        var parameters = new GetBulkDownloadLinkParams
        {
            WorkspaceExternalId = workspaceExternalId,
            FileExternalIds = files,
            FolderExternalIds = folders,
            ExcludedFileExternalIds = excludedFileExternalIds ?? [],
            ExcludedFolderExternalIds = excludedFolderExternalIds ?? [],
            ExpiresInMinutes = expiresInMinutes
        };

        var definition = AgentToolCatalog.TryGet(AgentToolNames.GetBulkDownloadLink)!;

        var workspaceOverride = workspaceToolOverrideReader.TryGet(
            agentId: agent.Id,
            workspaceId: workspace.Id,
            toolName: AgentToolNames.GetBulkDownloadLink);

        var effective = AgentToolCatalog.Resolve(
            agent,
            definition,
            workspaceOverride);

        if (!effective.IsUsable)
            throw new McpException("The get_bulk_download_link tool is not enabled for this workspace.");

        if (effective.RequiresApproval)
        {
            var expiresAt = clock.UtcNow.AddHours(
                operationsOptions.ApprovalWindowHours);

            var operationId = await operationLedger.CreatePending(
                agentId: agent.Id,
                workspaceId: workspace.Id,
                toolName: AgentToolNames.GetBulkDownloadLink,
                paramsJson: Json.Serialize(parameters),
                expiresAt: expiresAt,
                cancellationToken: cancellationToken);

            return AgentToolResponse.WaitsForApproval(
                approvalRequestId: operationId.Value,
                expiresAt: expiresAt);
        }

        var result = await getBulkDownloadLinkOperation.Execute(
            httpContext,
            parameters,
            cancellationToken);

        return AgentToolResponse.Executed(
            result);
    }
}
