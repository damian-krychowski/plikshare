using Microsoft.AspNetCore.Http;
using ModelContextProtocol;
using PlikShare.Agents.BoxAccess;
using PlikShare.Agents.Middleware;
using PlikShare.AuditLog;
using PlikShare.Boxes.Cache;
using PlikShare.Boxes.Id;
using PlikShare.Core.Clock;
using PlikShare.Core.Utils;
using PlikShare.Files.Download;
using PlikShare.Files.Id;
using PlikShare.Mcp.BoxAccess.DownloadLink.Contracts;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.Mcp.BoxAccess.DownloadLink;

/// <summary>
/// The reusable core of get_box_file_download_link: re-validates the agent's box access and creates a
/// short-lived pre-signed link to download a single file from the box (scoped to the box's subtree),
/// writing the audit entry. Called directly by the tool when no approval is required, and by the execute
/// flow once a human has approved the operation. The link's lifetime starts when it is created, so the
/// expiry is computed here (at execute time) from the stored duration.
/// </summary>
public class GetBoxFileDownloadLinkAgentOperation(
    BoxCache boxCache,
    AgentBoxAccessCache boxAccessCache,
    GetFileDownloadLinkOperation getFileDownloadLinkOperation,
    AuditLogService auditLogService,
    IClock clock)
{
    private const int DefaultExpiryMinutes = 15;
    private const int MinExpiryMinutes = 1;
    private const int MaxExpiryMinutes = 24 * 60;

    public async Task<GetBoxFileDownloadLinkResponseDto> Execute(
        HttpContext httpContext,
        GetBoxFileDownloadLinkParams parameters,
        CancellationToken cancellationToken)
    {
        var agent = await httpContext.GetAgentContext();

        var boxAccess = await httpContext.GetAgentBoxAccess(
            agent,
            boxCache,
            boxAccessCache,
            BoxExtId.Parse(parameters.BoxExternalId),
            cancellationToken);

        if (boxAccess.IsOff)
            throw new McpException(
                $"Box '{parameters.BoxExternalId}' is disabled or exposes no folder, so its files cannot be downloaded.");

        var workspace = boxAccess.Box.Workspace;
        var fileExternalId = FileExtId.Parse(parameters.FileExternalId);

        var effectiveMinutes = Math.Clamp(
            parameters.ExpiresInMinutes ?? DefaultExpiryMinutes,
            MinExpiryMinutes,
            MaxExpiryMinutes);

        var expiresAt = clock.UtcNow.AddMinutes(effectiveMinutes);

        var result = await getFileDownloadLinkOperation.Execute(
            workspace: workspace,
            fileExternalId: fileExternalId,
            contentDisposition: ContentDispositionType.Attachment,
            boxFolderId: boxAccess.Box.Folder!.Id,
            boxLinkId: null,
            userIdentity: boxAccess.UserIdentity,
            enforceInternalPassThrough: false,
            workspaceEncryptionSession: null,
            expiresAt: expiresAt,
            cancellationToken: cancellationToken);

        switch (result.Code)
        {
            case GetFileDownloadLinkOperation.ResultCode.Ok:
                await auditLogService.LogWithFileContext(
                    fileExternalId: fileExternalId,
                    buildEntry: fileRef => Audit.File.DownloadLinkGeneratedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        workspace: workspace.ToAuditLogWorkspaceRef(),
                        file: fileRef,
                        box: boxAccess.ToAuditLogBoxRef()),
                    cancellationToken);

                return new GetBoxFileDownloadLinkResponseDto
                {
                    Url = result.DownloadPreSignedUrl!,
                    ExpiresAt = expiresAt
                };

            case GetFileDownloadLinkOperation.ResultCode.FileNotFound:
                throw new McpException(
                    $"File '{parameters.FileExternalId}' was not found inside box '{parameters.BoxExternalId}'.");

            default:
                throw new McpException(
                    $"Could not create a download link for file '{parameters.FileExternalId}': {result.Code}.");
        }
    }
}
