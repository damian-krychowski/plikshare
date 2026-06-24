using Microsoft.AspNetCore.Http;
using ModelContextProtocol;
using PlikShare.Agents.BoxAccess;
using PlikShare.Agents.Middleware;
using PlikShare.AuditLog;
using PlikShare.Boxes.Cache;
using PlikShare.Boxes.Id;
using PlikShare.Core.Clock;
using PlikShare.Files.BulkDownload;
using PlikShare.Mcp.BoxAccess.BulkDownloadLink.Contracts;
using Audit = PlikShare.AuditLog.Details.Audit;
using BulkDownloadRequest = PlikShare.Files.BulkDownload.Contracts.GetBulkDownloadLinkRequestDto;

namespace PlikShare.Mcp.BoxAccess.BulkDownloadLink;

/// <summary>
/// The reusable core of get_box_bulk_download_link: re-validates the agent's box access and creates a
/// short-lived pre-signed link that downloads the selected files and folders from the box as a single ZIP
/// archive (scoped to the box's subtree), writing the audit entry. Called directly by the tool when no
/// approval is required, and by the execute flow once a human has approved the operation. The link's
/// lifetime starts when it is created, so the expiry is computed here (at execute time).
/// </summary>
public class GetBoxBulkDownloadLinkAgentOperation(
    BoxCache boxCache,
    AgentBoxAccessCache boxAccessCache,
    GetBulkDownloadLinkOperation getBulkDownloadLinkOperation,
    AuditLogService auditLogService,
    IClock clock)
{
    private const int DefaultExpiryMinutes = 15;
    private const int MinExpiryMinutes = 1;
    private const int MaxExpiryMinutes = 24 * 60;

    public async Task<GetBoxBulkDownloadLinkResponseDto> Execute(
        HttpContext httpContext,
        GetBoxBulkDownloadLinkParams parameters,
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

        var files = parameters.FileExternalIds.ToList();
        var folders = parameters.FolderExternalIds.ToList();

        if (files.Count == 0 && folders.Count == 0)
            throw new McpException("Provide at least one id in fileExternalIds or folderExternalIds.");

        var effectiveMinutes = Math.Clamp(
            parameters.ExpiresInMinutes ?? DefaultExpiryMinutes,
            MinExpiryMinutes,
            MaxExpiryMinutes);

        var expiresAt = clock.UtcNow.AddMinutes(effectiveMinutes);

        var result = getBulkDownloadLinkOperation.Execute(
            workspace: workspace,
            request: new BulkDownloadRequest
            {
                SelectedFiles = files,
                SelectedFolders = folders,
                ExcludedFiles = parameters.ExcludedFileExternalIds.ToList(),
                ExcludedFolders = parameters.ExcludedFolderExternalIds.ToList()
            },
            userIdentity: boxAccess.UserIdentity,
            boxFolderId: boxAccess.Box.Folder!.Id,
            boxLinkId: null,
            workspaceEncryptionSession: null,
            expiresAt: expiresAt);

        switch (result.Code)
        {
            case GetBulkDownloadLinkOperation.ResultCode.Ok:
                await auditLogService.Log(
                    Audit.File.BulkDownloadLinkGeneratedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        workspace: workspace.ToAuditLogWorkspaceRef(),
                        selectedFileExternalIds: files,
                        selectedFolderExternalIds: folders,
                        box: boxAccess.ToAuditLogBoxRef()),
                    cancellationToken);

                return new GetBoxBulkDownloadLinkResponseDto
                {
                    Url = result.PreSignedUrl!,
                    ExpiresAt = expiresAt
                };

            case GetBulkDownloadLinkOperation.ResultCode.FilesNotFound:
                throw new McpException(
                    $"These files were not found inside box '{parameters.BoxExternalId}': " +
                    $"{string.Join(", ", result.NotFoundFileExternalIds ?? [])}.");

            case GetBulkDownloadLinkOperation.ResultCode.FoldersNotFound:
                throw new McpException(
                    $"These folders were not found inside box '{parameters.BoxExternalId}': " +
                    $"{string.Join(", ", result.NotFoundFolderExternalIds ?? [])}.");

            default:
                throw new McpException(
                    $"Could not create a bulk download link: {result.Code}.");
        }
    }
}
