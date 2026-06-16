using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using PlikShare.AuditLog;
using PlikShare.BulkDelete;
using PlikShare.Core.CorrelationId;
using PlikShare.Core.UserIdentity;
using PlikShare.Files.Id;
using PlikShare.Folders.Id;
using PlikShare.Mcp.BulkDelete.Contracts;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.Id;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.Mcp.BulkDelete;

[McpServerToolType]
public class BulkDeleteTool
{
    [McpServerTool(Name = "bulk_delete")]
    [Description("Deletes files and/or folders in a workspace the agent can access. Each listed folder is " +
                 "deleted together with everything inside it (all subfolders and files), like 'rm -rf'. " +
                 "Provide at least one id in folderExternalIds or fileExternalIds. If the workspace has a " +
                 "trash policy enabled the deleted files can be restored from trash; otherwise the deletion " +
                 "is permanent. Returns the number and total size of files that were deleted.")]
    public static async Task<BulkDeleteResponseDto> Execute(
        IHttpContextAccessor httpContextAccessor,
        WorkspaceAgentMembershipCache workspaceAgentMembershipCache,
        BulkDeleteQuery bulkDeleteQuery,
        AuditLogService auditLogService,
        [Description("External id of the workspace.")]
        string workspaceExternalId,
        [Description("External ids of folders to delete, together with their entire contents. Optional if files are given.")]
        string[]? folderExternalIds = null,
        [Description("External ids of files to delete. Optional if folders are given.")]
        string[]? fileExternalIds = null,
        CancellationToken cancellationToken = default)
    {
        var httpContext = httpContextAccessor.HttpContext!;

        var membership = await httpContext.GetWorkspaceAgentMembershipDetails(
            workspaceAgentMembershipCache,
            WorkspaceExtId.Parse(workspaceExternalId),
            cancellationToken);

        var workspace = membership.Workspace;
        workspace.ThrowIfFullyEncrypted();

        var folders = (folderExternalIds ?? []).ToList();
        var files = (fileExternalIds ?? []).ToList();

        if (folders.Count == 0 && files.Count == 0)
            throw new McpException(
                "Provide at least one id in folderExternalIds or fileExternalIds.");

        var itemsContext = auditLogService.GetBulkItemsContext(
            folderExternalIds: folders,
            fileExternalIds: files,
            fileUploadExternalIds: []);

        var result = await bulkDeleteQuery.Execute(
            workspace: workspace,
            fileExternalIds: files.Select(FileExtId.Parse).ToArray(),
            folderExternalIds: folders.Select(FolderExtId.Parse).ToArray(),
            fileUploadExternalIds: [],
            boxFolderId: null,
            userIdentity: new AgentIdentity(membership.Agent.ExternalId),
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

        return new BulkDeleteResponseDto
        {
            DeletedFileCount = result.DeletedFileCount,
            DeletedSizeInBytes = result.DeletedSizeInBytes
        };
    }
}
