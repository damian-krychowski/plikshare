using Microsoft.AspNetCore.Http;
using ModelContextProtocol;
using PlikShare.AuditLog;
using PlikShare.Files.Id;
using PlikShare.Folders.Id;
using PlikShare.Folders.MoveToFolder;
using PlikShare.Mcp.MoveItems.Contracts;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.Id;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.Mcp.MoveItems;

/// <summary>
/// The reusable core of move_items: re-validates the agent's workspace access and moves the files
/// and folders into the destination, writing the audit entry. Called directly by the tool when no
/// approval is required, and by the execute flow once a human has approved the operation.
/// </summary>
public class MoveItemsAgentOperation(
    WorkspaceAgentMembershipCache workspaceAgentMembershipCache,
    MoveItemsToFolderQuery moveItemsToFolderQuery,
    AuditLogService auditLogService)
{
    public async Task<MoveItemsResponseDto> Execute(
        HttpContext httpContext,
        MoveItemsParams parameters,
        CancellationToken cancellationToken)
    {
        var membership = await httpContext.GetWorkspaceAgentMembershipDetails(
            workspaceAgentMembershipCache,
            WorkspaceExtId.Parse(parameters.WorkspaceExternalId),
            cancellationToken);

        var workspace = membership.Workspace;
        workspace.ThrowIfFullyEncrypted();

        var folderIds = parameters.FolderExternalIds.Select(FolderExtId.Parse).ToList();
        var fileIds = parameters.FileExternalIds.Select(FileExtId.Parse).ToList();

        if (folderIds.Count == 0 && fileIds.Count == 0)
            throw new McpException(
                "Provide at least one id in folderExternalIds or fileExternalIds.");

        var destinationFolder = string.IsNullOrWhiteSpace(parameters.DestinationFolderExternalId)
            ? (FolderExtId?)null
            : FolderExtId.Parse(parameters.DestinationFolderExternalId);

        var itemsContext = auditLogService.GetItemsMovedContext(
            destinationFolderExternalId: destinationFolder,
            folderExternalIds: folderIds,
            fileExternalIds: fileIds,
            fileUploadExternalIds: []);

        var resultCode = await moveItemsToFolderQuery.Execute(
            workspace: workspace,
            folderExternalIds: folderIds.ToArray(),
            fileExternalIds: fileIds.ToArray(),
            fileUploadExternalIds: [],
            destinationFolderExternalId: destinationFolder,
            destinationPosition: null,
            boxFolderId: null,
            cancellationToken: cancellationToken);

        switch (resultCode)
        {
            case MoveItemsToFolderQuery.ResultCode.Ok:
                await auditLogService.Log(
                    Audit.Folder.ItemsMovedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        workspace: workspace.ToAuditLogWorkspaceRef(),
                        destinationFolder: itemsContext.DestinationFolder,
                        folders: itemsContext.Folders,
                        files: itemsContext.Files,
                        fileUploads: itemsContext.FileUploads),
                    cancellationToken);

                return new MoveItemsResponseDto
                {
                    MovedFolderCount = folderIds.Count,
                    MovedFileCount = fileIds.Count,
                    DestinationFolderExternalId = destinationFolder?.Value
                };

            case MoveItemsToFolderQuery.ResultCode.DestinationFolderNotFound:
                throw new McpException(
                    $"Destination folder '{parameters.DestinationFolderExternalId}' was not found in workspace '{parameters.WorkspaceExternalId}'.");

            case MoveItemsToFolderQuery.ResultCode.FilesNotFound:
                throw new McpException(
                    $"Some of the files were not found in workspace '{parameters.WorkspaceExternalId}'.");

            case MoveItemsToFolderQuery.ResultCode.FoldersNotFound:
                throw new McpException(
                    $"Some of the folders were not found in workspace '{parameters.WorkspaceExternalId}'.");

            case MoveItemsToFolderQuery.ResultCode.FoldersMovedToOwnSubfolder:
                throw new McpException(
                    "A folder cannot be moved into itself or one of its own subfolders.");

            default:
                throw new McpException(
                    $"Unexpected result while moving items: {resultCode}.");
        }
    }
}
