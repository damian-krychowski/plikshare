using Microsoft.AspNetCore.Http;
using ModelContextProtocol;
using PlikShare.AuditLog;
using PlikShare.Core.UserIdentity;
using PlikShare.Folders.Create;
using PlikShare.Folders.Id;
using PlikShare.Mcp.Folders.Create.Contracts;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.Id;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.Mcp.Folders.Create;

/// <summary>
/// The reusable core of create_folder: re-validates the agent's workspace access and creates the
/// folder (with the id fixed at submit time), writing the audit entry. Called directly by the tool
/// when no approval is required, and by the execute flow once a human has approved the operation.
/// </summary>
public class CreateFolderAgentOperation(
    WorkspaceAgentMembershipCache workspaceAgentMembershipCache,
    CreateFolderQuery createFolderQuery,
    AuditLogService auditLogService)
{
    public async Task<CreateFolderResponseDto> Execute(
        HttpContext httpContext,
        CreateFolderParams parameters,
        CancellationToken cancellationToken)
    {
        var membership = await httpContext.GetWorkspaceAgentMembershipDetails(
            workspaceAgentMembershipCache,
            WorkspaceExtId.Parse(parameters.WorkspaceExternalId),
            cancellationToken);

        var workspace = membership.Workspace;
        workspace.ThrowIfFullyEncrypted();

        var folderExternalId = FolderExtId.Parse(parameters.FolderExternalId);

        FolderExtId? parentExternalId = string.IsNullOrWhiteSpace(parameters.ParentFolderExternalId)
            ? null
            : FolderExtId.Parse(parameters.ParentFolderExternalId);

        var result = await createFolderQuery.Execute(
            workspace: workspace,
            folderExternalId: folderExternalId,
            parentFolderExternalId: parentExternalId,
            name: workspace.EncodeMetadata(
                value: parameters.Name,
                workspaceEncryptionSession: null),
            boxFolderId: null,
            userIdentity: new AgentIdentity(membership.Agent.ExternalId),
            cancellationToken: cancellationToken);

        switch (result)
        {
            case CreateFolderQuery.ResultCode.Ok:
                await auditLogService.LogWithFolderContext(
                    folderExternalId: folderExternalId,
                    buildEntry: folderRef => Audit.Folder.CreatedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        workspace: workspace.ToAuditLogWorkspaceRef(),
                        folder: folderRef),
                    cancellationToken);

                return new CreateFolderResponseDto
                {
                    FolderExternalId = folderExternalId.Value,
                    Name = parameters.Name,
                    ParentFolderExternalId = parentExternalId?.Value
                };

            case CreateFolderQuery.ResultCode.ParentFolderNotFound:
                throw new McpException(
                    $"Parent folder '{parameters.ParentFolderExternalId}' was not found in workspace '{parameters.WorkspaceExternalId}'.");

            default:
                throw new McpException(
                    $"Unexpected result while creating folder: {result}.");
        }
    }
}
