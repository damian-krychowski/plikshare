using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using PlikShare.Agents.Tools;
using PlikShare.AuditLog;
using PlikShare.Mcp.ShareLinks.Get.Contracts;
using PlikShare.QuickShares;
using PlikShare.QuickShares.Cache;
using PlikShare.QuickShares.Get;
using PlikShare.QuickShares.Id;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.Id;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.Mcp.ShareLinks.Get;

[McpServerToolType]
public class GetShareLinkTool
{
    [McpServerTool(Name = AgentToolNames.GetShareLink)]
    [Description("Returns the details of a single share link in a workspace the agent can access: its public " +
                 "URL, expiration, download limits, whether it is password protected, the agent that created " +
                 "it (if any), and exactly which files and folders are shared and excluded.")]
    public static async Task<GetShareLinkResponseDto> Execute(
        IHttpContextAccessor httpContextAccessor,
        WorkspaceAgentMembershipCache workspaceAgentMembershipCache,
        QuickShareCache quickShareCache,
        GetQuickShareItemsQuery getQuickShareItemsQuery,
        QuickShareUrlBuilder urlBuilder,
        AuditLogService auditLogService,
        [Description("External id of the workspace.")]
        string workspaceExternalId,
        [Description("External id of the share link.")]
        string shareLinkExternalId,
        CancellationToken cancellationToken = default)
    {
        var httpContext = httpContextAccessor.HttpContext!;

        var membership = await httpContext.GetWorkspaceAgentMembershipDetails(
            workspaceAgentMembershipCache,
            WorkspaceExtId.Parse(workspaceExternalId),
            cancellationToken);

        var workspace = membership.Workspace;
        workspace.ThrowIfFullyEncrypted();

        var quickShare = await quickShareCache.TryGetQuickShare(
            new QuickShareExtId(shareLinkExternalId),
            cancellationToken);

        if (quickShare is null || quickShare.Workspace.Id != workspace.Id)
            throw new McpException(
                $"Share link '{shareLinkExternalId}' was not found in workspace '{workspaceExternalId}'.");

        var items = getQuickShareItemsQuery.Execute(quickShare.Id);

        await auditLogService.Log(
            Audit.Agent.ShareLinkViewedEntry(
                actor: httpContext.GetAuditLogActorContext(),
                workspaceExternalId: workspaceExternalId,
                shareLinkExternalId: shareLinkExternalId),
            cancellationToken);

        return new GetShareLinkResponseDto
        {
            ExternalId = quickShare.ExternalId.Value,
            Name = quickShare.Name,
            Url = quickShare.SecretHash is null
                ? urlBuilder.BuildUrl(quickShare.Slug)
                : null,
            CreatedAt = quickShare.CreatedAt,
            ExpiresAt = quickShare.ExpiresAt,
            MaxDownloads = quickShare.MaxDownloads,
            DownloadsCount = quickShare.DownloadsCount,
            HasPassword = quickShare.PasswordHash is not null,
            CreatedByAgentExternalId = quickShare.CreatorAgentExternalId?.Value,
            SelectedFileExternalIds = items.SelectedFiles.Select(x => x.Value).ToList(),
            SelectedFolderExternalIds = items.SelectedFolders.Select(x => x.Value).ToList(),
            ExcludedFileExternalIds = items.ExcludedFiles.Select(x => x.Value).ToList(),
            ExcludedFolderExternalIds = items.ExcludedFolders.Select(x => x.Value).ToList()
        };
    }
}
