using Microsoft.AspNetCore.Http;
using ModelContextProtocol;
using PlikShare.AuditLog;
using PlikShare.Mcp.Workspaces.Members.UpdatePermissions.Contracts;
using PlikShare.Users.Cache;
using PlikShare.Users.Id;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.Id;
using PlikShare.Workspaces.Members.UpdatePermissions;
using PlikShare.Workspaces.Permissions;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.Mcp.Workspaces.Members.UpdatePermissions;

/// <summary>
/// The reusable core of update_workspace_member_permissions: re-validates the agent's workspace access
/// and updates the member's permissions, writing the audit entry. Called directly by the tool when no
/// approval is required, and by the execute flow once a human has approved the operation.
/// </summary>
public class UpdateWorkspaceMemberPermissionsAgentOperation(
    WorkspaceAgentMembershipCache workspaceAgentMembershipCache,
    UpdateWorkspaceMemberPermissionsQuery updateWorkspaceMemberPermissionsQuery,
    UserCache userCache,
    AuditLogService auditLogService)
{
    public async Task<UpdateWorkspaceMemberPermissionsResponseDto> Execute(
        HttpContext httpContext,
        UpdateWorkspaceMemberPermissionsParams parameters,
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

        var resultCode = await updateWorkspaceMemberPermissionsQuery.Execute(
            workspace: workspace,
            member: member,
            permissions: new WorkspacePermissions(
                AllowShare: parameters.AllowShare),
            cancellationToken: cancellationToken);

        switch (resultCode)
        {
            case UpdateWorkspaceMemberPermissionsQuery.ResultCode.Ok:
                await auditLogService.LogWithStorageContext(
                    storageExternalId: workspace.Storage.ExternalId,
                    buildEntry: storageRef => Audit.Workspace.MemberPermissionsUpdatedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        storage: storageRef,
                        workspace: workspace.ToAuditLogWorkspaceRef(),
                        member: member.ToAuditLogUserRef(),
                        allowShare: parameters.AllowShare),
                    cancellationToken);

                return new UpdateWorkspaceMemberPermissionsResponseDto
                {
                    MemberExternalId = parameters.MemberExternalId,
                    AllowShare = parameters.AllowShare
                };

            case UpdateWorkspaceMemberPermissionsQuery.ResultCode.NotFound:
                throw new McpException(
                    $"Member '{parameters.MemberExternalId}' was not found in workspace '{parameters.WorkspaceExternalId}'.");

            default:
                throw new McpException(
                    $"Unexpected result while updating member permissions: {resultCode}.");
        }
    }
}
