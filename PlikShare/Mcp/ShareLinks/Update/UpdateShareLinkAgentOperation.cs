using Microsoft.AspNetCore.Http;
using ModelContextProtocol;
using PlikShare.AuditLog;
using PlikShare.Mcp.ShareLinks.Update.Contracts;
using PlikShare.QuickShares.Cache;
using PlikShare.QuickShares.Id;
using PlikShare.QuickShares.Update;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.Id;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.Mcp.ShareLinks.Update;

/// <summary>
/// The reusable core of update_share_link: re-validates the agent's workspace access, finds the share
/// link and applies the requested setting changes, invalidating the cache and writing the audit entry.
/// Called directly by the tool when no approval is required, and by the execute flow once a human has
/// approved the operation. The password is already hashed at submit time.
/// </summary>
public class UpdateShareLinkAgentOperation(
    WorkspaceAgentMembershipCache workspaceAgentMembershipCache,
    QuickShareCache quickShareCache,
    UpdateQuickShareQuery updateQuickShareQuery,
    AuditLogService auditLogService)
{
    public async Task<UpdateShareLinkResponseDto> Execute(
        HttpContext httpContext,
        UpdateShareLinkParams parameters,
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

        var resultCode = await updateQuickShareQuery.Execute(
            quickShare: quickShare,
            updateName: parameters.UpdateName,
            name: parameters.Name,
            updateExpiration: parameters.UpdateExpiration,
            expiresAt: parameters.ExpiresAt,
            updateMaxDownloads: parameters.UpdateMaxDownloads,
            maxDownloads: parameters.MaxDownloads,
            updatePassword: parameters.UpdatePassword,
            passwordHashBase64: parameters.PasswordHashBase64,
            passwordSalt: parameters.PasswordSalt,
            cancellationToken: cancellationToken);

        switch (resultCode)
        {
            case UpdateQuickShareQuery.ResultCode.Ok:
                await quickShareCache.InvalidateEntry(
                    quickShareId: quickShare.Id,
                    cancellationToken: cancellationToken);

                await auditLogService.Log(
                    Audit.QuickShare.UpdatedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        workspace: quickShare.Workspace.ToAuditLogWorkspaceRef(),
                        quickShare: quickShare.ToAuditLogQuickShareRef(),
                        nameUpdated: parameters.UpdateName,
                        expirationUpdated: parameters.UpdateExpiration,
                        expiresAt: parameters.ExpiresAt,
                        maxDownloadsUpdated: parameters.UpdateMaxDownloads,
                        maxDownloads: parameters.MaxDownloads,
                        passwordUpdated: parameters.UpdatePassword,
                        passwordSet: parameters.PasswordSet),
                    cancellationToken);

                return new UpdateShareLinkResponseDto
                {
                    ExternalId = parameters.ShareLinkExternalId
                };

            case UpdateQuickShareQuery.ResultCode.NotFound:
                throw new McpException(
                    $"Share link '{parameters.ShareLinkExternalId}' was not found in workspace '{parameters.WorkspaceExternalId}'.");

            default:
                throw new McpException($"Could not update the share link: {resultCode}.");
        }
    }
}
