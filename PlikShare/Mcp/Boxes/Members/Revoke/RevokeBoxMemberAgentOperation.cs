using Microsoft.AspNetCore.Http;
using ModelContextProtocol;
using PlikShare.AuditLog;
using PlikShare.Boxes.Cache;
using PlikShare.Boxes.Id;
using PlikShare.Boxes.Members.Revoke;
using PlikShare.Core.CorrelationId;
using PlikShare.Mcp.Boxes.Members.Revoke.Contracts;
using PlikShare.Users.Id;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.Id;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.Mcp.Boxes.Members.Revoke;

/// <summary>
/// The reusable core of revoke_box_member: re-validates the agent's workspace access, resolves the box
/// membership within that workspace and removes the member, invalidating the membership cache and writing
/// the audit entry. Called directly by the tool when no approval is required, and by the execute flow once
/// a human has approved the operation.
/// </summary>
public class RevokeBoxMemberAgentOperation(
    WorkspaceAgentMembershipCache workspaceAgentMembershipCache,
    BoxMembershipCache boxMembershipCache,
    RevokeBoxMemberQuery revokeBoxMemberQuery,
    AuditLogService auditLogService)
{
    public async Task<RevokeBoxMemberResponseDto> Execute(
        HttpContext httpContext,
        RevokeBoxMemberParams parameters,
        CancellationToken cancellationToken)
    {
        var membership = await httpContext.GetWorkspaceAgentMembershipDetails(
            workspaceAgentMembershipCache,
            WorkspaceExtId.Parse(parameters.WorkspaceExternalId),
            cancellationToken);

        var workspace = membership.Workspace;
        workspace.ThrowIfFullyEncrypted();

        var boxMembership = await httpContext.GetAgentBoxMembershipInWorkspace(
            boxMembershipCache,
            workspace,
            BoxExtId.Parse(parameters.BoxExternalId),
            UserExtId.Parse(parameters.MemberExternalId),
            cancellationToken);

        var code = await revokeBoxMemberQuery.Execute(
            boxMembership: boxMembership,
            correlationId: httpContext.GetCorrelationId(),
            cancellationToken: cancellationToken);

        switch (code)
        {
            case RevokeBoxMemberQuery.ResultCode.Ok:
                await boxMembershipCache.InvalidateEntry(boxMembership, cancellationToken);

                await auditLogService.LogWithFolderContext(
                    folderExternalId: boxMembership.Box.Folder?.ExternalId,
                    buildEntry: folderRef => Audit.Box.MemberRevokedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        workspace: workspace.ToAuditLogWorkspaceRef(),
                        box: new Audit.BoxRef
                        {
                            ExternalId = boxMembership.Box.ExternalId,
                            Name = boxMembership.Box.Name,
                            Folder = folderRef
                        },
                        member: boxMembership.Member.ToAuditLogUserRef()),
                    cancellationToken);

                return new RevokeBoxMemberResponseDto
                {
                    MemberExternalId = parameters.MemberExternalId
                };

            case RevokeBoxMemberQuery.ResultCode.MembershipNotFound:
                throw new McpException(
                    $"Member '{parameters.MemberExternalId}' was not found in box '{parameters.BoxExternalId}'.");

            default:
                throw new McpException($"Could not revoke the box member: {code}.");
        }
    }
}
