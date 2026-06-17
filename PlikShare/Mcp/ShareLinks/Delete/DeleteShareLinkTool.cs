using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using PlikShare.Agents.Tools;
using PlikShare.AuditLog;
using PlikShare.QuickShares.Cache;
using PlikShare.QuickShares.Delete;
using PlikShare.QuickShares.Id;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.Id;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.Mcp.ShareLinks.Delete;

[McpServerToolType]
public class DeleteShareLinkTool
{
    [McpServerTool(Name = AgentToolNames.DeleteShareLink)]
    [Description("Deletes a share link in a workspace the agent can access. The public URL stops working " +
                 "immediately. The shared files and folders themselves are not deleted.")]
    public static async Task Execute(
        IHttpContextAccessor httpContextAccessor,
        WorkspaceAgentMembershipCache workspaceAgentMembershipCache,
        QuickShareCache quickShareCache,
        DeleteQuickShareQuery deleteQuickShareQuery,
        AuditLogService auditLogService,
        [Description("External id of the workspace.")]
        string workspaceExternalId,
        [Description("External id of the share link to delete.")]
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

        var resultCode = await deleteQuickShareQuery.Execute(
            quickShare: quickShare,
            cancellationToken: cancellationToken);

        if (resultCode != DeleteQuickShareQuery.ResultCode.Ok)
            throw new McpException(
                $"Share link '{shareLinkExternalId}' was not found in workspace '{workspaceExternalId}'.");

        await quickShareCache.InvalidateEntry(
            quickShareId: quickShare.Id,
            cancellationToken: cancellationToken);

        await auditLogService.Log(
            Audit.QuickShare.DeletedEntry(
                actor: httpContext.GetAuditLogActorContext(),
                workspace: quickShare.Workspace.ToAuditLogWorkspaceRef(),
                quickShare: quickShare.ToAuditLogQuickShareRef()),
            cancellationToken);
    }
}
