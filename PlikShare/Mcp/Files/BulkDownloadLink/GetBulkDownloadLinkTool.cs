using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using PlikShare.Agents.Tools;
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

[McpServerToolType]
public class GetBulkDownloadLinkTool
{
    private const int DefaultExpiryMinutes = 15;
    private const int MinExpiryMinutes = 1;
    private const int MaxExpiryMinutes = 24 * 60;

    [McpServerTool(Name = AgentToolNames.GetBulkDownloadLink)]
    [Description("Creates a short-lived link that downloads the selected files and/or folders from a workspace " +
                 "as a single ZIP archive, to hand to a user. Folders are included with all their contents. " +
                 "Provide at least one id in fileExternalIds or folderExternalIds. Anyone with the link can " +
                 "download without logging in until it expires, so keep the expiry short.")]
    public static async Task<GetBulkDownloadLinkResponseDto> Execute(
        IHttpContextAccessor httpContextAccessor,
        WorkspaceAgentMembershipCache workspaceAgentMembershipCache,
        GetBulkDownloadLinkOperation getBulkDownloadLinkOperation,
        AuditLogService auditLogService,
        IClock clock,
        [Description("External id of the workspace.")]
        string workspaceExternalId,
        [Description("External ids of files to include. Optional if folders are given.")]
        string[]? fileExternalIds = null,
        [Description("External ids of folders to include, downloaded with all their contents. Optional if files are given.")]
        string[]? folderExternalIds = null,
        [Description("Optional external ids of files to carve out of the included folders.")]
        string[]? excludedFileExternalIds = null,
        [Description("Optional external ids of subfolders to carve out of the included folders.")]
        string[]? excludedFolderExternalIds = null,
        [Description("How long the link stays valid, in minutes. Default 15, minimum 1, maximum 1440 (24h).")]
        int? expiresInMinutes = null,
        CancellationToken cancellationToken = default)
    {
        var httpContext = httpContextAccessor.HttpContext!;

        var membership = await httpContext.GetWorkspaceAgentMembershipDetails(
            workspaceAgentMembershipCache,
            WorkspaceExtId.Parse(workspaceExternalId),
            cancellationToken);

        var workspace = membership.Workspace;
        workspace.ThrowIfFullyEncrypted();

        var files = (fileExternalIds ?? []).ToList();
        var folders = (folderExternalIds ?? []).ToList();
        var excludedFiles = (excludedFileExternalIds ?? []).ToList();
        var excludedFolders = (excludedFolderExternalIds ?? []).ToList();

        if (files.Count == 0 && folders.Count == 0)
            throw new McpException("Provide at least one id in fileExternalIds or folderExternalIds.");

        var effectiveMinutes = Math.Clamp(
            expiresInMinutes ?? DefaultExpiryMinutes,
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
                        workspaceExternalId: workspaceExternalId,
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
                    $"These files were not found in workspace '{workspaceExternalId}': " +
                    $"{string.Join(", ", result.NotFoundFileExternalIds ?? [])}.");

            case GetBulkDownloadLinkOperation.ResultCode.FoldersNotFound:
                throw new McpException(
                    $"These folders were not found in workspace '{workspaceExternalId}': " +
                    $"{string.Join(", ", result.NotFoundFolderExternalIds ?? [])}.");

            default:
                throw new McpException(
                    $"Could not create a bulk download link: {result.Code}.");
        }
    }
}
