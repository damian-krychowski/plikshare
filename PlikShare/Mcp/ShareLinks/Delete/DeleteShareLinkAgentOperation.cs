using Microsoft.AspNetCore.Http;
using ModelContextProtocol;
using PlikShare.AuditLog;
using PlikShare.Mcp.ShareLinks.Delete.Contracts;
using PlikShare.QuickShares.Cache;
using PlikShare.QuickShares.Delete;
using PlikShare.QuickShares.Id;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.Id;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.Mcp.ShareLinks.Delete;

/// <summary>
/// The reusable core of delete_share_link: re-validates the agent's workspace access, finds the
/// share link and deletes it, writing the audit entry. Called directly by the tool when no approval
/// is required, and by the execute flow once a human has approved the operation.
/// </summary>
public class DeleteShareLinkAgentOperation(
    WorkspaceAgentMembershipCache workspaceAgentMembershipCache,
    QuickShareCache quickShareCache,
    DeleteQuickShareQuery deleteQuickShareQuery,
    AuditLogService auditLogService)
{
    public async Task<DeleteShareLinkResponseDto> Execute(
        HttpContext httpContext,
        DeleteShareLinkParams parameters,
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

        var resultCode = await deleteQuickShareQuery.Execute(
            quickShare: quickShare,
            cancellationToken: cancellationToken);

        if (resultCode != DeleteQuickShareQuery.ResultCode.Ok)
            throw new McpException(
                $"Share link '{parameters.ShareLinkExternalId}' was not found in workspace '{parameters.WorkspaceExternalId}'.");

        await quickShareCache.InvalidateEntry(
            quickShareId: quickShare.Id,
            cancellationToken: cancellationToken);

        await auditLogService.Log(
            Audit.QuickShare.DeletedEntry(
                actor: httpContext.GetAuditLogActorContext(),
                workspace: quickShare.Workspace.ToAuditLogWorkspaceRef(),
                quickShare: quickShare.ToAuditLogQuickShareRef()),
            cancellationToken);

        return new DeleteShareLinkResponseDto
        {
            ExternalId = quickShare.ExternalId.Value,
            Name = quickShare.Name
        };
    }
}
