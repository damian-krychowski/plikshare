using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using PlikShare.Agents.Tools;
using PlikShare.AuditLog;
using PlikShare.Files.Id;
using PlikShare.Folders.Id;
using PlikShare.Folders.MoveToFolder;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.Id;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.Mcp.MoveItems;

[McpServerToolType]
public class MoveItemsTool
{
    [McpServerTool(Name = AgentToolNames.MoveItems)]
    [Description("Moves files and/or folders into another folder within the same workspace. Each moved " +
                 "folder is moved together with its entire contents. Provide at least one id in " +
                 "folderExternalIds or fileExternalIds. Omit destinationFolderExternalId to move the items " +
                 "to the workspace root. All items and the destination must belong to the given workspace; " +
                 "moving items between workspaces is not supported.")]
    public static async Task Execute(
        IHttpContextAccessor httpContextAccessor,
        WorkspaceAgentMembershipCache workspaceAgentMembershipCache,
        MoveItemsToFolderQuery moveItemsToFolderQuery,
        AuditLogService auditLogService,
        [Description("External id of the workspace that contains the items and the destination folder.")]
        string workspaceExternalId,
        [Description("External ids of folders to move, together with their entire contents. Optional if files are given.")]
        string[]? folderExternalIds = null,
        [Description("External ids of files to move. Optional if folders are given.")]
        string[]? fileExternalIds = null,
        [Description("External id of the destination folder. Omit to move the items to the workspace root.")]
        string? destinationFolderExternalId = null,
        CancellationToken cancellationToken = default)
    {
        var httpContext = httpContextAccessor.HttpContext!;

        var membership = await httpContext.GetWorkspaceAgentMembershipDetails(
            workspaceAgentMembershipCache,
            WorkspaceExtId.Parse(workspaceExternalId),
            cancellationToken);

        var workspace = membership.Workspace;
        workspace.ThrowIfFullyEncrypted();

        var folderIds = (folderExternalIds ?? []).Select(FolderExtId.Parse).ToList();
        var fileIds = (fileExternalIds ?? []).Select(FileExtId.Parse).ToList();

        if (folderIds.Count == 0 && fileIds.Count == 0)
            throw new McpException(
                "Provide at least one id in folderExternalIds or fileExternalIds.");

        var destinationFolder = string.IsNullOrWhiteSpace(destinationFolderExternalId)
            ? (FolderExtId?)null
            : FolderExtId.Parse(destinationFolderExternalId);

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

                return;

            case MoveItemsToFolderQuery.ResultCode.DestinationFolderNotFound:
                throw new McpException(
                    $"Destination folder '{destinationFolderExternalId}' was not found in workspace '{workspaceExternalId}'.");

            case MoveItemsToFolderQuery.ResultCode.FilesNotFound:
                throw new McpException(
                    $"Some of the files were not found in workspace '{workspaceExternalId}'.");

            case MoveItemsToFolderQuery.ResultCode.FoldersNotFound:
                throw new McpException(
                    $"Some of the folders were not found in workspace '{workspaceExternalId}'.");

            case MoveItemsToFolderQuery.ResultCode.FoldersMovedToOwnSubfolder:
                throw new McpException(
                    "A folder cannot be moved into itself or one of its own subfolders.");

            default:
                throw new McpException(
                    $"Unexpected result while moving items: {resultCode}.");
        }
    }
}
