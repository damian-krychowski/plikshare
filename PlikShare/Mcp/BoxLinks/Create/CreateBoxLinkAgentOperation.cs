using Microsoft.AspNetCore.Http;
using ModelContextProtocol;
using PlikShare.AuditLog;
using PlikShare.Boxes.Cache;
using PlikShare.Boxes.CreateLink;
using PlikShare.Boxes.Id;
using PlikShare.Mcp.BoxLinks.Create.Contracts;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.Id;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.Mcp.BoxLinks.Create;

/// <summary>
/// The reusable core of create_box_link: re-validates the agent's workspace access, resolves the box
/// within that workspace and creates a public link to it, writing the audit entry. Called directly by the
/// tool when no approval is required, and by the execute flow once a human has approved the operation.
/// </summary>
public class CreateBoxLinkAgentOperation(
    WorkspaceAgentMembershipCache workspaceAgentMembershipCache,
    BoxCache boxCache,
    CreateBoxLinkQuery createBoxLinkQuery,
    AuditLogService auditLogService)
{
    public async Task<CreateBoxLinkResponseDto> Execute(
        HttpContext httpContext,
        CreateBoxLinkParams parameters,
        CancellationToken cancellationToken)
    {
        var membership = await httpContext.GetWorkspaceAgentMembershipDetails(
            workspaceAgentMembershipCache,
            WorkspaceExtId.Parse(parameters.WorkspaceExternalId),
            cancellationToken);

        var workspace = membership.Workspace;
        workspace.ThrowIfFullyEncrypted();

        var box = await httpContext.GetAgentBoxInWorkspace(
            boxCache,
            workspace,
            BoxExtId.Parse(parameters.BoxExternalId),
            cancellationToken);

        var trimmedName = (parameters.Name ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(trimmedName))
            throw new McpException("A name for the box link is required.");

        var result = await createBoxLinkQuery.Execute(
            box: box,
            name: trimmedName,
            cancellationToken: cancellationToken);

        switch (result.Code)
        {
            case CreateBoxLinkQuery.ResultCode.Ok:
                await auditLogService.Log(
                    Audit.Box.LinkCreatedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        workspace: workspace.ToAuditLogWorkspaceRef(),
                        box: new Audit.BoxRef { ExternalId = box.ExternalId, Name = box.Name },
                        linkExternalId: result.BoxLink.ExternalId,
                        linkName: trimmedName),
                    cancellationToken);

                return new CreateBoxLinkResponseDto
                {
                    ExternalId = result.BoxLink.ExternalId.Value,
                    AccessCode = result.BoxLink.AccessCode
                };

            case CreateBoxLinkQuery.ResultCode.BoxNotFound:
                throw new McpException(
                    $"Box '{parameters.BoxExternalId}' was not found in workspace '{parameters.WorkspaceExternalId}'.");

            default:
                throw new McpException($"Could not create the box link: {result.Code}.");
        }
    }
}
