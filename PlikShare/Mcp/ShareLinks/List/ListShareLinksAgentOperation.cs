using Microsoft.AspNetCore.Http;
using PlikShare.AuditLog;
using PlikShare.Mcp.ShareLinks.List.Contracts;
using PlikShare.QuickShares.List;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.Id;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.Mcp.ShareLinks.List;

/// <summary>
/// The reusable core of list_share_links: re-validates the agent's workspace access and lists the
/// workspace's share links, writing the audit entry. Called directly by the tool when no approval is
/// required, and by the execute flow once a human has approved the operation. The read is idempotent,
/// so the execute flow simply re-lists.
/// </summary>
public class ListShareLinksAgentOperation(
    WorkspaceAgentMembershipCache workspaceAgentMembershipCache,
    GetQuickSharesQuery getQuickSharesQuery,
    AuditLogService auditLogService)
{
    public async Task<ListShareLinksResponseDto> Execute(
        HttpContext httpContext,
        ListShareLinksParams parameters,
        CancellationToken cancellationToken)
    {
        var membership = await httpContext.GetWorkspaceAgentMembershipDetails(
            workspaceAgentMembershipCache,
            WorkspaceExtId.Parse(parameters.WorkspaceExternalId),
            cancellationToken);

        var workspace = membership.Workspace;
        workspace.ThrowIfFullyEncrypted();

        var list = getQuickSharesQuery.Execute(workspace);

        await auditLogService.Log(
            Audit.Agent.ShareLinksListedEntry(
                actor: httpContext.GetAuditLogActorContext(),
                workspaceExternalId: parameters.WorkspaceExternalId,
                count: list.Items.Count),
            cancellationToken);

        return new ListShareLinksResponseDto
        {
            ShareLinks = list.Items
                .Select(item => new ShareLinkListItemDto
                {
                    ExternalId = item.ExternalId.Value,
                    Name = item.Name,
                    Url = item.Url,
                    CreatedAt = item.CreatedAt,
                    ExpiresAt = item.ExpiresAt,
                    DownloadsCount = item.DownloadsCount,
                    MaxDownloads = item.MaxDownloads,
                    HasPassword = item.HasPassword,
                    SelectedFilesCount = item.SelectedFilesCount,
                    SelectedFoldersCount = item.SelectedFoldersCount
                })
                .ToList()
        };
    }
}
