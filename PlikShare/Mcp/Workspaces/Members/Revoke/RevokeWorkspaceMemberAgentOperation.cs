using Microsoft.AspNetCore.Http;
using ModelContextProtocol;
using PlikShare.AuditLog;
using PlikShare.Core.CorrelationId;
using PlikShare.Mcp.Workspaces.Members.Revoke.Contracts;
using PlikShare.Users.Cache;
using PlikShare.Users.Id;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.Id;
using PlikShare.Workspaces.Members.Revoke;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.Mcp.Workspaces.Members.Revoke;

/// <summary>
/// The reusable core of revoke_workspace_member: re-validates the agent's workspace access and removes
/// the member, invalidating the membership and user caches and writing the audit entry. Called directly
/// by the tool when no approval is required, and by the execute flow once a human has approved the
/// operation.
/// </summary>
public class RevokeWorkspaceMemberAgentOperation(
    WorkspaceAgentMembershipCache workspaceAgentMembershipCache,
    WorkspaceMembershipCache workspaceMembershipCache,
    RevokeWorkspaceMemberQuery revokeWorkspaceMemberQuery,
    UserCache userCache,
    AuditLogService auditLogService)
{
    public async Task<RevokeWorkspaceMemberResponseDto> Execute(
        HttpContext httpContext,
        RevokeWorkspaceMemberParams parameters,
        CancellationToken cancellationToken)
    {
        var membership = await httpContext.GetWorkspaceAgentMembershipDetails(
            workspaceAgentMembershipCache,
            WorkspaceExtId.Parse(parameters.WorkspaceExternalId),
            cancellationToken);

        var workspace = membership.Workspace;
        workspace.ThrowIfFullyEncrypted();

        var member = await userCache.TryGetUser(
            UserExtId.Parse(parameters.MemberExternalId),
            cancellationToken)
            ?? throw new McpException(
                $"Member '{parameters.MemberExternalId}' was not found in workspace '{parameters.WorkspaceExternalId}'.");

        var resultCode = await revokeWorkspaceMemberQuery.Execute(
            workspace: workspace,
            member: member,
            correlationId: httpContext.GetCorrelationId(),
            cancellationToken: cancellationToken);

        switch (resultCode)
        {
            case RevokeWorkspaceMemberQuery.ResultCode.Ok:
                await workspaceMembershipCache.InvalidateEntry(
                    workspaceId: workspace.Id,
                    memberId: member.Id,
                    cancellationToken: cancellationToken);

                await userCache.InvalidateEntry(
                    member.Id,
                    cancellationToken: cancellationToken);

                await auditLogService.LogWithStorageContext(
                    storageExternalId: workspace.Storage.ExternalId,
                    buildEntry: storageRef => Audit.Workspace.MemberRevokedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        storage: storageRef,
                        workspace: workspace.ToAuditLogWorkspaceRef(),
                        member: member.ToAuditLogUserRef()),
                    cancellationToken);

                return new RevokeWorkspaceMemberResponseDto
                {
                    MemberExternalId = parameters.MemberExternalId
                };

            case RevokeWorkspaceMemberQuery.ResultCode.MembershipNotFound:
                throw new McpException(
                    $"Member '{parameters.MemberExternalId}' was not found in workspace '{parameters.WorkspaceExternalId}'.");

            default:
                throw new McpException(
                    $"Unexpected result while revoking the workspace member: {resultCode}.");
        }
    }
}
