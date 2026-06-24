using Microsoft.AspNetCore.Http;
using ModelContextProtocol;
using PlikShare.Agents.BoxAccess;
using PlikShare.Agents.Middleware;
using PlikShare.AuditLog;
using PlikShare.Boxes.Cache;
using PlikShare.Boxes.Id;
using PlikShare.BulkDelete;
using PlikShare.Core.CorrelationId;
using PlikShare.Files.Id;
using PlikShare.Folders.Id;
using PlikShare.Mcp.BoxAccess.Delete.Contracts;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.Mcp.BoxAccess.Delete;

/// <summary>
/// The reusable core of delete_box_items: re-validates the agent's box access and deletes the given files
/// and/or folders (scoped to the box's subtree), writing the audit entry. Called directly by the tool when
/// no approval is required, and by the commit flow once a human has approved the operation.
/// </summary>
public class DeleteBoxItemsAgentOperation(
    BoxCache boxCache,
    AgentBoxAccessCache boxAccessCache,
    BulkDeleteQuery bulkDeleteQuery,
    AuditLogService auditLogService)
{
    public async Task<DeleteBoxItemsResponseDto> Execute(
        HttpContext httpContext,
        DeleteBoxItemsParams parameters,
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
                $"Box '{parameters.BoxExternalId}' is disabled or exposes no folder, so its content cannot be deleted.");

        var workspace = boxAccess.Box.Workspace;

        var folders = parameters.FolderExternalIds.ToList();
        var files = parameters.FileExternalIds.ToList();

        if (folders.Count == 0 && files.Count == 0)
            throw new McpException("Provide at least one id in folderExternalIds or fileExternalIds.");

        var itemsContext = auditLogService.GetBulkItemsContext(
            folderExternalIds: folders,
            fileExternalIds: files,
            fileUploadExternalIds: []);

        var result = await bulkDeleteQuery.Execute(
            workspace: workspace,
            fileExternalIds: files.Select(FileExtId.Parse).ToArray(),
            folderExternalIds: folders.Select(FolderExtId.Parse).ToArray(),
            fileUploadExternalIds: [],
            boxFolderId: boxAccess.Box.Folder!.Id,
            userIdentity: boxAccess.UserIdentity,
            isFileDeleteAllowedByBoxPermissions: true,
            correlationId: httpContext.GetCorrelationId(),
            cancellationToken: cancellationToken);

        await auditLogService.LogWithStorageContext(
            storageExternalId: workspace.Storage.ExternalId,
            buildEntry: storageRef => Audit.Workspace.BulkDeleteRequestedEntry(
                actor: httpContext.GetAuditLogActorContext(),
                storage: storageRef,
                workspace: workspace.ToAuditLogWorkspaceRef(),
                files: itemsContext.Files,
                folders: itemsContext.Folders,
                fileUploads: itemsContext.FileUploads),
            cancellationToken);

        return new DeleteBoxItemsResponseDto
        {
            DeletedFileCount = result.DeletedFileCount,
            DeletedSizeInBytes = result.DeletedSizeInBytes
        };
    }
}
