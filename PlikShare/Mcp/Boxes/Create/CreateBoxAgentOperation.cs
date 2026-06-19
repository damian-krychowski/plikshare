using Microsoft.AspNetCore.Http;
using ModelContextProtocol;
using PlikShare.AuditLog;
using PlikShare.Boxes.Create;
using PlikShare.Folders.Id;
using PlikShare.Mcp.Boxes.Create.Contracts;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.Id;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.Mcp.Boxes.Create;

/// <summary>
/// The reusable core of create_box: re-validates the agent's workspace access and creates the box on the
/// given folder, writing the audit entry. Called directly by the tool when no approval is required, and
/// by the execute flow once a human has approved the operation.
/// </summary>
public class CreateBoxAgentOperation(
    WorkspaceAgentMembershipCache workspaceAgentMembershipCache,
    CreateBoxQuery createBoxQuery,
    AuditLogService auditLogService)
{
    public async Task<CreateBoxResponseDto> Execute(
        HttpContext httpContext,
        CreateBoxParams parameters,
        CancellationToken cancellationToken)
    {
        var membership = await httpContext.GetWorkspaceAgentMembershipDetails(
            workspaceAgentMembershipCache,
            WorkspaceExtId.Parse(parameters.WorkspaceExternalId),
            cancellationToken);

        var workspace = membership.Workspace;
        workspace.ThrowIfFullyEncrypted();

        var trimmedName = (parameters.Name ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(trimmedName))
            throw new McpException("A name for the box is required.");

        var result = await createBoxQuery.Execute(
            workspace: workspace,
            name: trimmedName,
            folderExternalId: FolderExtId.Parse(parameters.FolderExternalId),
            cancellationToken: cancellationToken);

        switch (result.Code)
        {
            case CreateBoxQuery.ResultCode.Ok:
                await auditLogService.Log(
                    Audit.Box.CreatedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        workspace: workspace.ToAuditLogWorkspaceRef(),
                        box: new Audit.BoxRef
                        {
                            ExternalId = result.BoxExternalId,
                            Name = trimmedName
                        }),
                    cancellationToken);

                return new CreateBoxResponseDto
                {
                    BoxExternalId = result.BoxExternalId.Value,
                    Name = trimmedName
                };

            case CreateBoxQuery.ResultCode.FolderWasNotFound:
                throw new McpException(
                    $"Folder '{parameters.FolderExternalId}' was not found in the workspace.");

            default:
                throw new McpException($"Could not create the box: {result.Code}.");
        }
    }
}
