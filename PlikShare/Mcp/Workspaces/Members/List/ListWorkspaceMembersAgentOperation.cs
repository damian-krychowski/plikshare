using Microsoft.AspNetCore.Http;
using PlikShare.AuditLog;
using PlikShare.Mcp.Workspaces.Members.List.Contracts;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.Id;
using PlikShare.Workspaces.Members.List;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.Mcp.Workspaces.Members.List;

/// <summary>
/// The reusable core of list_workspace_members: re-validates the agent's workspace access and lists the
/// workspace's members, writing the audit entry. Called directly by the tool when no approval is
/// required, and by the execute flow once a human has approved the operation. The read is idempotent, so
/// the execute flow simply re-lists.
/// </summary>
public class ListWorkspaceMembersAgentOperation(
    WorkspaceAgentMembershipCache workspaceAgentMembershipCache,
    GetWorkspaceMembersListQuery getWorkspaceMembersListQuery,
    AuditLogService auditLogService)
{
    public async Task<ListWorkspaceMembersResponseDto> Execute(
        HttpContext httpContext,
        ListWorkspaceMembersParams parameters,
        CancellationToken cancellationToken)
    {
        var membership = await httpContext.GetWorkspaceAgentMembershipDetails(
            workspaceAgentMembershipCache,
            WorkspaceExtId.Parse(parameters.WorkspaceExternalId),
            cancellationToken);

        var workspace = membership.Workspace;
        workspace.ThrowIfFullyEncrypted();

        var list = getWorkspaceMembersListQuery.Execute(
            workspace: workspace,
            cancellationToken: cancellationToken);

        await auditLogService.Log(
            Audit.Agent.WorkspaceMembersListedEntry(
                actor: httpContext.GetAuditLogActorContext(),
                workspaceExternalId: parameters.WorkspaceExternalId,
                count: list.Items.Count),
            cancellationToken);

        return new ListWorkspaceMembersResponseDto
        {
            Members = list.Items
                .Select(item => new ListWorkspaceMembersResponseDto.WorkspaceMemberDto
                {
                    MemberExternalId = item.MemberExternalId.Value,
                    Email = item.MemberEmail,
                    InviterEmail = item.InviterEmail,
                    InvitationAccepted = item.WasInvitationAccepted,
                    AllowShare = item.Permissions.AllowShare
                })
                .ToList()
        };
    }
}
