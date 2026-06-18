using Microsoft.AspNetCore.Http;
using ModelContextProtocol;
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

/// <summary>
/// The reusable core of bulk_delete: resolves the agent's workspace membership (re-validating
/// access), performs the delete and writes the audit entry. Called directly by the tool when no
/// approval is required, and by the commit flow once a human has approved the operation.
/// </summary>
public class BulkDeleteAgentOperation(
    WorkspaceAgentMembershipCache workspaceAgentMembershipCache,
    BulkDeleteQuery bulkDeleteQuery,
    AuditLogService auditLogService)
{
    public async Task<BulkDeleteResponseDto> Execute(
        HttpContext httpContext,
        BulkDeleteParams parameters,
        CancellationToken cancellationToken)
    {
        var membership = await httpContext.GetWorkspaceAgentMembershipDetails(
            workspaceAgentMembershipCache,
            WorkspaceExtId.Parse(parameters.WorkspaceExternalId),
            cancellationToken);

        var workspace = membership.Workspace;
        workspace.ThrowIfFullyEncrypted();

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
