using Microsoft.AspNetCore.Http;
using ModelContextProtocol;
using PlikShare.AuditLog;
using PlikShare.Core.UserIdentity;
using PlikShare.Files.Id;
using PlikShare.Files.Rename;
using PlikShare.Mcp.Files.Rename.Contracts;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.Id;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.Mcp.Files.Rename;

/// <summary>
/// The reusable core of rename_file: re-validates the agent's workspace access and renames the file
/// (keeping its extension), writing the audit entry. Called directly by the tool when no approval is
/// required, and by the execute flow once a human has approved the operation.
/// </summary>
public class RenameFileAgentOperation(
    WorkspaceAgentMembershipCache workspaceAgentMembershipCache,
    UpdateFileNameQuery updateFileNameQuery,
    AuditLogService auditLogService)
{
    public async Task<RenameFileResponseDto> Execute(
        HttpContext httpContext,
        RenameFileParams parameters,
        CancellationToken cancellationToken)
    {
        var membership = await httpContext.GetWorkspaceAgentMembershipDetails(
            workspaceAgentMembershipCache,
            WorkspaceExtId.Parse(parameters.WorkspaceExternalId),
            cancellationToken);

        var workspace = membership.Workspace;
        workspace.ThrowIfFullyEncrypted();

        var fileId = FileExtId.Parse(parameters.FileExternalId);

        var resultCode = await updateFileNameQuery.Execute(
            workspace: workspace,
            fileExternalId: fileId,
            name: workspace.EncodeMetadata(
                value: parameters.Name,
                workspaceEncryptionSession: null),
            boxFolderId: null,
            userIdentity: new AgentIdentity(membership.Agent.ExternalId),
            isRenameAllowedByBoxPermissions: true,
            cancellationToken: cancellationToken);

        switch (resultCode)
        {
            case UpdateFileNameQuery.ResultCode.Ok:
                await auditLogService.LogWithFileContext(
                    fileExternalId: fileId,
                    buildEntry: fileRef => Audit.File.RenamedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        workspace: workspace.ToAuditLogWorkspaceRef(),
                        file: fileRef),
                    cancellationToken);

                return new RenameFileResponseDto
                {
                    FileExternalId = parameters.FileExternalId,
                    Name = parameters.Name
                };

            case UpdateFileNameQuery.ResultCode.FileNotFound:
                throw new McpException(
                    $"File '{parameters.FileExternalId}' was not found in workspace '{parameters.WorkspaceExternalId}'.");

            default:
                throw new McpException(
                    $"Unexpected result while renaming file: {resultCode}.");
        }
    }
}
