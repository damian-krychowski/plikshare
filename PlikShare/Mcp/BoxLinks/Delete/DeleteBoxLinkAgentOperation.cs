using Microsoft.AspNetCore.Http;
using ModelContextProtocol;
using PlikShare.AuditLog;
using PlikShare.BoxLinks.Cache;
using PlikShare.BoxLinks.Delete;
using PlikShare.BoxLinks.Id;
using PlikShare.Mcp.BoxLinks.Delete.Contracts;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.Id;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.Mcp.BoxLinks.Delete;

/// <summary>
/// The reusable core of delete_box_link: re-validates the agent's workspace access, resolves the box link
/// within that workspace and deletes it, invalidating the box link cache and writing the audit entry.
/// Called directly by the tool when no approval is required, and by the execute flow once a human has
/// approved the operation.
/// </summary>
public class DeleteBoxLinkAgentOperation(
    WorkspaceAgentMembershipCache workspaceAgentMembershipCache,
    BoxLinkCache boxLinkCache,
    DeleteBoxLinkQuery deleteBoxLinkQuery,
    AuditLogService auditLogService)
{
    public async Task<DeleteBoxLinkResponseDto> Execute(
        HttpContext httpContext,
        DeleteBoxLinkParams parameters,
        CancellationToken cancellationToken)
    {
        var membership = await httpContext.GetWorkspaceAgentMembershipDetails(
            workspaceAgentMembershipCache,
            WorkspaceExtId.Parse(parameters.WorkspaceExternalId),
            cancellationToken);

        var workspace = membership.Workspace;
        workspace.ThrowIfFullyEncrypted();

        var boxLink = await httpContext.GetAgentBoxLinkInWorkspace(
            boxLinkCache,
            workspace,
            BoxLinkExtId.Parse(parameters.BoxLinkExternalId),
            cancellationToken);

        var code = await deleteBoxLinkQuery.Execute(boxLink, cancellationToken);

        switch (code)
        {
            case DeleteBoxLinkQuery.ResultCode.Ok:
                await boxLinkCache.InvalidateEntry(boxLink.Id, cancellationToken);

                await auditLogService.Log(
                    Audit.BoxLink.DeletedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        workspace: workspace.ToAuditLogWorkspaceRef(),
                        box: new Audit.BoxRef { ExternalId = boxLink.Box.ExternalId, Name = boxLink.Box.Name },
                        boxLink: new Audit.BoxLinkRef { ExternalId = boxLink.ExternalId, Name = boxLink.Name }),
                    cancellationToken);

                return new DeleteBoxLinkResponseDto
                {
                    BoxLinkExternalId = parameters.BoxLinkExternalId
                };

            case DeleteBoxLinkQuery.ResultCode.BoxLinkNotFound:
                throw new McpException(
                    $"Box link '{parameters.BoxLinkExternalId}' was not found in workspace '{parameters.WorkspaceExternalId}'.");

            default:
                throw new McpException($"Could not delete the box link: {code}.");
        }
    }
}
