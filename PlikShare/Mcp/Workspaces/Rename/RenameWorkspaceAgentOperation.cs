using Microsoft.AspNetCore.Http;
using ModelContextProtocol;
using PlikShare.AuditLog;
using PlikShare.Mcp.Workspaces.Rename.Contracts;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.Id;
using PlikShare.Workspaces.UpdateName;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.Mcp.Workspaces.Rename;

/// <summary>
/// The reusable core of rename_workspace: re-validates the agent's workspace access and renames the
/// workspace, invalidating the cache and writing the audit entry. Called directly by the tool when no
/// approval is required, and by the execute flow once a human has approved the operation.
/// </summary>
public class RenameWorkspaceAgentOperation(
    WorkspaceAgentMembershipCache workspaceAgentMembershipCache,
    WorkspaceCache workspaceCache,
    UpdateWorkspaceNameQuery updateWorkspaceNameQuery,
    AuditLogService auditLogService)
{
    public async Task<RenameWorkspaceResponseDto> Execute(
        HttpContext httpContext,
        RenameWorkspaceParams parameters,
        CancellationToken cancellationToken)
    {
        var membership = await httpContext.GetWorkspaceAgentMembershipDetails(
            workspaceAgentMembershipCache,
            WorkspaceExtId.Parse(parameters.WorkspaceExternalId),
            cancellationToken);

        var workspace = membership.Workspace;
        workspace.ThrowIfFullyEncrypted();

        var resultCode = await updateWorkspaceNameQuery.Execute(
            workspace: workspace,
            name: parameters.Name,
            cancellationToken: cancellationToken);

        switch (resultCode)
        {
            case UpdateWorkspaceNameQuery.ResultCode.Ok:
                await workspaceCache.InvalidateEntry(
                    workspace.ExternalId,
                    cancellationToken);

                await auditLogService.LogWithStorageContext(
                    storageExternalId: workspace.Storage.ExternalId,
                    buildEntry: storageRef => Audit.Workspace.NameUpdatedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        storage: storageRef,
                        workspace: new Audit.WorkspaceRef
                        {
                            ExternalId = workspace.ExternalId,
                            Name = parameters.Name
                        }),
                    cancellationToken);

                return new RenameWorkspaceResponseDto
                {
                    WorkspaceExternalId = parameters.WorkspaceExternalId,
                    Name = parameters.Name
                };

            case UpdateWorkspaceNameQuery.ResultCode.NotFound:
                throw new McpException(
                    $"Workspace '{parameters.WorkspaceExternalId}' was not found.");

            default:
                throw new McpException(
                    $"Unexpected result while renaming workspace: {resultCode}.");
        }
    }
}
