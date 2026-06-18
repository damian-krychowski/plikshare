using Microsoft.AspNetCore.Http;
using ModelContextProtocol;
using PlikShare.AuditLog;
using PlikShare.Core.UserIdentity;
using PlikShare.Folders.Id;
using PlikShare.Folders.Rename;
using PlikShare.Mcp.Folders.Rename.Contracts;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.Id;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.Mcp.Folders.Rename;

/// <summary>
/// The reusable core of rename_folder: re-validates the agent's workspace access and renames the
/// folder, writing the audit entry. Called directly by the tool when no approval is required, and by
/// the execute flow once a human has approved the operation.
/// </summary>
public class RenameFolderAgentOperation(
    WorkspaceAgentMembershipCache workspaceAgentMembershipCache,
    UpdateFolderNameQuery updateFolderNameQuery,
    AuditLogService auditLogService)
{
    public async Task<RenameFolderResponseDto> Execute(
        HttpContext httpContext,
        RenameFolderParams parameters,
        CancellationToken cancellationToken)
    {
        var membership = await httpContext.GetWorkspaceAgentMembershipDetails(
            workspaceAgentMembershipCache,
            WorkspaceExtId.Parse(parameters.WorkspaceExternalId),
            cancellationToken);

        var workspace = membership.Workspace;
        workspace.ThrowIfFullyEncrypted();

        var folderId = FolderExtId.Parse(parameters.FolderExternalId);

        var resultCode = await updateFolderNameQuery.Execute(
            workspace: workspace,
            folderExternalId: folderId,
            name: workspace.EncodeMetadata(
                value: parameters.Name,
                workspaceEncryptionSession: null),
            boxFolderId: null,
            userIdentity: new AgentIdentity(membership.Agent.ExternalId),
            isOperationAllowedByBoxPermissions: true,
            cancellationToken: cancellationToken);

        switch (resultCode)
        {
            case UpdateFolderNameQuery.ResultCode.Ok:
                await auditLogService.LogWithFolderContext(
                    folderExternalId: folderId,
                    buildEntry: folderRef => Audit.Folder.NameUpdatedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        workspace: workspace.ToAuditLogWorkspaceRef(),
                        folder: folderRef),
                    cancellationToken);

                return new RenameFolderResponseDto
                {
                    FolderExternalId = parameters.FolderExternalId,
                    Name = parameters.Name
                };

            case UpdateFolderNameQuery.ResultCode.FolderNotFound:
                throw new McpException(
                    $"Folder '{parameters.FolderExternalId}' was not found in workspace '{parameters.WorkspaceExternalId}'.");

            default:
                throw new McpException(
                    $"Unexpected result while renaming folder: {resultCode}.");
        }
    }
}
