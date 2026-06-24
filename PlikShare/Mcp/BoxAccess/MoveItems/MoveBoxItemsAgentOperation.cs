using Microsoft.AspNetCore.Http;
using ModelContextProtocol;
using PlikShare.Agents.BoxAccess;
using PlikShare.Agents.Middleware;
using PlikShare.AuditLog;
using PlikShare.Boxes.Cache;
using PlikShare.Boxes.Id;
using PlikShare.Files.Id;
using PlikShare.Folders.Id;
using PlikShare.Folders.MoveToFolder;
using PlikShare.Mcp.BoxAccess.MoveItems.Contracts;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.Mcp.BoxAccess.MoveItems;

/// <summary>
/// The reusable core of move_box_items: re-validates the agent's box access and moves the files and folders
/// into the destination folder (defaulting to the box root, scoped to the box's subtree), writing the audit
/// entry. Called directly by the tool when no approval is required, and by the execute flow once a human has
/// approved the operation.
/// </summary>
public class MoveBoxItemsAgentOperation(
    BoxCache boxCache,
    AgentBoxAccessCache boxAccessCache,
    MoveItemsToFolderQuery moveItemsToFolderQuery,
    AuditLogService auditLogService)
{
    public async Task<MoveBoxItemsResponseDto> Execute(
        HttpContext httpContext,
        MoveBoxItemsParams parameters,
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
                $"Box '{parameters.BoxExternalId}' is disabled or exposes no folder, so its content cannot be changed.");

        var workspace = boxAccess.Box.Workspace;
        var boxFolder = boxAccess.Box.Folder!;

        var folderIds = parameters.FolderExternalIds.Select(FolderExtId.Parse).ToList();
        var fileIds = parameters.FileExternalIds.Select(FileExtId.Parse).ToList();

        if (folderIds.Count == 0 && fileIds.Count == 0)
            throw new McpException(
                "Provide at least one id in folderExternalIds or fileExternalIds.");

        var destinationFolder = string.IsNullOrWhiteSpace(parameters.DestinationFolderExternalId)
            ? boxFolder.ExternalId
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
            boxFolderId: boxFolder.Id,
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
                        fileUploads: itemsContext.FileUploads,
                        box: boxAccess.ToAuditLogBoxRef()),
                    cancellationToken);

                return new MoveBoxItemsResponseDto
                {
                    MovedFolderCount = folderIds.Count,
                    MovedFileCount = fileIds.Count,
                    DestinationFolderExternalId = destinationFolder.Value
                };

            case MoveItemsToFolderQuery.ResultCode.DestinationFolderNotFound:
                throw new McpException(
                    $"Destination folder '{destinationFolder}' was not found inside box '{parameters.BoxExternalId}'.");

            case MoveItemsToFolderQuery.ResultCode.FilesNotFound:
                throw new McpException(
                    $"Some of the files were not found inside box '{parameters.BoxExternalId}'.");

            case MoveItemsToFolderQuery.ResultCode.FoldersNotFound:
                throw new McpException(
                    $"Some of the folders were not found inside box '{parameters.BoxExternalId}'.");

            case MoveItemsToFolderQuery.ResultCode.FoldersMovedToOwnSubfolder:
                throw new McpException(
                    "A folder cannot be moved into itself or one of its own subfolders.");

            default:
                throw new McpException(
                    $"Unexpected result while moving items: {resultCode}.");
        }
    }
}
