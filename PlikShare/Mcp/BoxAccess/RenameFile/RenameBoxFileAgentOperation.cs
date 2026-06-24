using Microsoft.AspNetCore.Http;
using ModelContextProtocol;
using PlikShare.Agents.BoxAccess;
using PlikShare.Agents.Middleware;
using PlikShare.AuditLog;
using PlikShare.Boxes.Cache;
using PlikShare.Boxes.Id;
using PlikShare.Files.Id;
using PlikShare.Files.Rename;
using PlikShare.Mcp.BoxAccess.RenameFile.Contracts;
using PlikShare.Workspaces.Cache;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.Mcp.BoxAccess.RenameFile;

/// <summary>
/// The reusable core of rename_box_file: re-validates the agent's box access and renames a file inside the
/// box (keeping its extension, scoped to the box's subtree), writing the audit entry. Called directly by
/// the tool when no approval is required, and by the execute flow once a human has approved the operation.
/// </summary>
public class RenameBoxFileAgentOperation(
    BoxCache boxCache,
    AgentBoxAccessCache boxAccessCache,
    UpdateFileNameQuery updateFileNameQuery,
    AuditLogService auditLogService)
{
    public async Task<RenameBoxFileResponseDto> Execute(
        HttpContext httpContext,
        RenameBoxFileParams parameters,
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
        var fileExternalId = FileExtId.Parse(parameters.FileExternalId);

        var resultCode = await updateFileNameQuery.Execute(
            workspace: workspace,
            fileExternalId: fileExternalId,
            name: workspace.EncodeMetadata(
                value: parameters.Name,
                workspaceEncryptionSession: null),
            boxFolderId: boxAccess.Box.Folder!.Id,
            userIdentity: boxAccess.UserIdentity,
            isRenameAllowedByBoxPermissions: true,
            cancellationToken: cancellationToken);

        switch (resultCode)
        {
            case UpdateFileNameQuery.ResultCode.Ok:
                await auditLogService.LogWithFileContext(
                    fileExternalId: fileExternalId,
                    buildEntry: fileRef => Audit.File.RenamedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        workspace: workspace.ToAuditLogWorkspaceRef(),
                        file: fileRef,
                        box: boxAccess.ToAuditLogBoxRef()),
                    cancellationToken);

                return new RenameBoxFileResponseDto
                {
                    FileExternalId = parameters.FileExternalId,
                    Name = parameters.Name
                };

            case UpdateFileNameQuery.ResultCode.FileNotFound:
                throw new McpException(
                    $"File '{parameters.FileExternalId}' was not found inside box '{parameters.BoxExternalId}'.");

            default:
                throw new McpException(
                    $"Unexpected result while renaming file: {resultCode}.");
        }
    }
}
