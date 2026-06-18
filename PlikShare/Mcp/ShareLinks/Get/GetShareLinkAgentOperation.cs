using Microsoft.AspNetCore.Http;
using ModelContextProtocol;
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

/// <summary>
/// The reusable core of get_share_link: re-validates the agent's workspace access, finds the share
/// link and returns its details, writing the audit entry. Called directly by the tool when no approval
/// is required, and by the execute flow once a human has approved the operation. The read is
/// idempotent, so the execute flow simply re-reads.
/// </summary>
public class GetShareLinkAgentOperation(
    WorkspaceAgentMembershipCache workspaceAgentMembershipCache,
    QuickShareCache quickShareCache,
    GetQuickShareItemsQuery getQuickShareItemsQuery,
    QuickShareUrlBuilder urlBuilder,
    AuditLogService auditLogService)
{
    public async Task<GetShareLinkResponseDto> Execute(
        HttpContext httpContext,
        GetShareLinkParams parameters,
        CancellationToken cancellationToken)
    {
        var membership = await httpContext.GetWorkspaceAgentMembershipDetails(
            workspaceAgentMembershipCache,
            WorkspaceExtId.Parse(parameters.WorkspaceExternalId),
            cancellationToken);

        var workspace = membership.Workspace;
        workspace.ThrowIfFullyEncrypted();

        var quickShare = await quickShareCache.TryGetQuickShare(
            new QuickShareExtId(parameters.ShareLinkExternalId),
            cancellationToken);

        if (quickShare is null || quickShare.Workspace.Id != workspace.Id)
            throw new McpException(
                $"Share link '{parameters.ShareLinkExternalId}' was not found in workspace '{parameters.WorkspaceExternalId}'.");

        var items = getQuickShareItemsQuery.Execute(quickShare.Id);

        await auditLogService.Log(
            Audit.Agent.ShareLinkViewedEntry(
                actor: httpContext.GetAuditLogActorContext(),
                workspaceExternalId: parameters.WorkspaceExternalId,
                shareLinkExternalId: parameters.ShareLinkExternalId),
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
