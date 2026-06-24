using Microsoft.AspNetCore.Http;
using ModelContextProtocol;
using PlikShare.Agents.BoxAccess;
using PlikShare.Agents.Middleware;
using PlikShare.AuditLog;
using PlikShare.Boxes.Cache;
using PlikShare.Boxes.Id;
using PlikShare.Folders.Id;
using PlikShare.Folders.Rename;
using PlikShare.Mcp.BoxAccess.RenameFolder.Contracts;
using PlikShare.Workspaces.Cache;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.Mcp.BoxAccess.RenameFolder;

/// <summary>
/// The reusable core of rename_box_folder: re-validates the agent's box access and renames a folder inside
/// the box (scoped to the box's subtree), writing the audit entry. Called directly by the tool when no
/// approval is required, and by the execute flow once a human has approved the operation.
/// </summary>
public class RenameBoxFolderAgentOperation(
    BoxCache boxCache,
    AgentBoxAccessCache boxAccessCache,
    UpdateFolderNameQuery updateFolderNameQuery,
    AuditLogService auditLogService)
{
    public async Task<RenameBoxFolderResponseDto> Execute(
        HttpContext httpContext,
        RenameBoxFolderParams parameters,
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
        var folderExternalId = FolderExtId.Parse(parameters.FolderExternalId);

        var resultCode = await updateFolderNameQuery.Execute(
            workspace: workspace,
            folderExternalId: folderExternalId,
            name: workspace.EncodeMetadata(
                value: parameters.Name,
                workspaceEncryptionSession: null),
            boxFolderId: boxAccess.Box.Folder!.Id,
            userIdentity: boxAccess.UserIdentity,
            isOperationAllowedByBoxPermissions: true,
            cancellationToken: cancellationToken);

        switch (resultCode)
        {
            case UpdateFolderNameQuery.ResultCode.Ok:
                await auditLogService.LogWithFolderContext(
                    folderExternalId: folderExternalId,
                    buildEntry: folderRef => Audit.Folder.NameUpdatedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        workspace: workspace.ToAuditLogWorkspaceRef(),
                        folder: folderRef,
                        box: boxAccess.ToAuditLogBoxRef()),
                    cancellationToken);

                return new RenameBoxFolderResponseDto
                {
                    FolderExternalId = parameters.FolderExternalId,
                    Name = parameters.Name
                };

            case UpdateFolderNameQuery.ResultCode.FolderNotFound:
                throw new McpException(
                    $"Folder '{parameters.FolderExternalId}' was not found inside box '{parameters.BoxExternalId}'.");

            default:
                throw new McpException(
                    $"Unexpected result while renaming folder: {resultCode}.");
        }
    }
}
