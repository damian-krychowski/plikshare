using Microsoft.AspNetCore.Http;
using ModelContextProtocol;
using PlikShare.AuditLog;
using PlikShare.BoxLinks.Cache;
using PlikShare.BoxLinks.Id;
using PlikShare.BoxLinks.RegenerateAccessCode;
using PlikShare.Mcp.BoxLinks.RegenerateAccessCode.Contracts;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.Id;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.Mcp.BoxLinks.RegenerateAccessCode;

/// <summary>
/// The reusable core of regenerate_box_link_access_code: re-validates the agent's workspace access,
/// resolves the box link within that workspace and regenerates its access code (invalidating the old URL),
/// invalidating the box link cache and writing the audit entry. Called directly by the tool when no
/// approval is required, and by the execute flow once a human has approved the operation.
/// </summary>
public class RegenerateBoxLinkAccessCodeAgentOperation(
    WorkspaceAgentMembershipCache workspaceAgentMembershipCache,
    BoxLinkCache boxLinkCache,
    RegenerateBoxLinkAccessCodeQuery regenerateBoxLinkAccessCodeQuery,
    AuditLogService auditLogService)
{
    public async Task<RegenerateBoxLinkAccessCodeResponseDto> Execute(
        HttpContext httpContext,
        RegenerateBoxLinkAccessCodeParams parameters,
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

        var result = await regenerateBoxLinkAccessCodeQuery.Execute(boxLink, cancellationToken);

        switch (result.Code)
        {
            case RegenerateBoxLinkAccessCodeQuery.ResultCode.Ok:
                await boxLinkCache.InvalidateEntry(boxLink.Id, cancellationToken);

                await auditLogService.Log(
                    Audit.BoxLink.AccessCodeRegeneratedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        workspace: workspace.ToAuditLogWorkspaceRef(),
                        box: new Audit.BoxRef { ExternalId = boxLink.Box.ExternalId, Name = boxLink.Box.Name },
                        boxLink: new Audit.BoxLinkRef { ExternalId = boxLink.ExternalId, Name = boxLink.Name }),
                    cancellationToken);

                return new RegenerateBoxLinkAccessCodeResponseDto
                {
                    BoxLinkExternalId = parameters.BoxLinkExternalId,
                    AccessCode = result.AccessCode!
                };

            case RegenerateBoxLinkAccessCodeQuery.ResultCode.BoxLinkNotFound:
                throw new McpException(
                    $"Box link '{parameters.BoxLinkExternalId}' was not found in workspace '{parameters.WorkspaceExternalId}'.");

            default:
                throw new McpException($"Could not regenerate the box link access code: {result.Code}.");
        }
    }
}
