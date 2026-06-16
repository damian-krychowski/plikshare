using System.ComponentModel;
using ModelContextProtocol.Server;
using PlikShare.AuditLog;
using PlikShare.Mcp.ShareLinks.List.Contracts;
using PlikShare.QuickShares.List;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.Id;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.Mcp.ShareLinks.List;

[McpServerToolType]
public class ListShareLinksTool
{
    [McpServerTool(Name = "list_share_links")]
    [Description("Lists all public share links in a workspace the agent can access, with their external ids, " +
                 "names, public URLs, expiration, download counts and how many files/folders each shares.")]
    public static async Task<ListShareLinksResponseDto> Execute(
        IHttpContextAccessor httpContextAccessor,
        WorkspaceAgentMembershipCache workspaceAgentMembershipCache,
        GetQuickSharesQuery getQuickSharesQuery,
        AuditLogService auditLogService,
        [Description("External id of the workspace.")]
        string workspaceExternalId,
        CancellationToken cancellationToken = default)
    {
        var httpContext = httpContextAccessor.HttpContext!;

        var membership = await httpContext.GetWorkspaceAgentMembershipDetails(
            workspaceAgentMembershipCache,
            WorkspaceExtId.Parse(workspaceExternalId),
            cancellationToken);

        var workspace = membership.Workspace;
        workspace.ThrowIfFullyEncrypted();

        var list = getQuickSharesQuery.Execute(workspace);

        await auditLogService.Log(
            Audit.Agent.ShareLinksListedEntry(
                actor: httpContext.GetAuditLogActorContext(),
                workspaceExternalId: workspaceExternalId,
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
