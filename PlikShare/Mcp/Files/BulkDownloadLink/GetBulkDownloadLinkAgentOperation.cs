using Microsoft.AspNetCore.Http;
using ModelContextProtocol;
using PlikShare.AuditLog;
using PlikShare.Core.Clock;
using PlikShare.Core.UserIdentity;
using PlikShare.Files.BulkDownload;
using PlikShare.Mcp.Files.BulkDownloadLink.Contracts;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.Id;
using Audit = PlikShare.AuditLog.Details.Audit;
using BulkDownloadRequest = PlikShare.Files.BulkDownload.Contracts.GetBulkDownloadLinkRequestDto;

namespace PlikShare.Mcp.Files.BulkDownloadLink;

/// <summary>
/// The reusable core of get_bulk_download_link: re-validates the agent's workspace access and creates a
/// short-lived pre-signed link that downloads the selected files and folders as a single ZIP archive,
/// writing the audit entry. Called directly by the tool when no approval is required, and by the execute
/// flow once a human has approved the operation. The link's lifetime starts when it is created, so the
/// expiry is computed here (at execute time) from the stored duration.
/// </summary>
public class GetBulkDownloadLinkAgentOperation(
    WorkspaceAgentMembershipCache workspaceAgentMembershipCache,
    GetBulkDownloadLinkOperation getBulkDownloadLinkOperation,
    AuditLogService auditLogService,
    IClock clock)
{
    private const int DefaultExpiryMinutes = 15;
    private const int MinExpiryMinutes = 1;
    private const int MaxExpiryMinutes = 24 * 60;

    public async Task<GetBulkDownloadLinkResponseDto> Execute(
        HttpContext httpContext,
        GetBulkDownloadLinkParams parameters,
        CancellationToken cancellationToken)
    {
        var membership = await httpContext.GetWorkspaceAgentMembershipDetails(
            workspaceAgentMembershipCache,
            WorkspaceExtId.Parse(parameters.WorkspaceExternalId),
            cancellationToken);

        var workspace = membership.Workspace;
        workspace.ThrowIfFullyEncrypted();

        var files = parameters.FileExternalIds.ToList();
        var folders = parameters.FolderExternalIds.ToList();
        var excludedFiles = parameters.ExcludedFileExternalIds.ToList();
        var excludedFolders = parameters.ExcludedFolderExternalIds.ToList();

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
                ExcludedFiles = excludedFiles,
                ExcludedFolders = excludedFolders
            },
            userIdentity: new AgentIdentity(membership.Agent.ExternalId),
            boxFolderId: null,
            boxLinkId: null,
            workspaceEncryptionSession: null,
            expiresAt: expiresAt);

        switch (result.Code)
        {
            case GetBulkDownloadLinkOperation.ResultCode.Ok:
                await auditLogService.Log(
                    Audit.Agent.BulkDownloadLinkGeneratedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        workspaceExternalId: parameters.WorkspaceExternalId,
                        selectedFileCount: files.Count,
                        selectedFolderCount: folders.Count,
                        expiresAt: expiresAt),
                    cancellationToken);

                return new GetBulkDownloadLinkResponseDto
                {
                    Url = result.PreSignedUrl!,
                    ExpiresAt = expiresAt
                };

            case GetBulkDownloadLinkOperation.ResultCode.FilesNotFound:
                throw new McpException(
                    $"These files were not found in workspace '{parameters.WorkspaceExternalId}': " +
                    $"{string.Join(", ", result.NotFoundFileExternalIds ?? [])}.");

            case GetBulkDownloadLinkOperation.ResultCode.FoldersNotFound:
                throw new McpException(
                    $"These folders were not found in workspace '{parameters.WorkspaceExternalId}': " +
                    $"{string.Join(", ", result.NotFoundFolderExternalIds ?? [])}.");

            default:
                throw new McpException(
                    $"Could not create a bulk download link: {result.Code}.");
        }
    }
}
