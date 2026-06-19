using Microsoft.AspNetCore.Http;
using PlikShare.AuditLog;
using PlikShare.Boxes.Cache;
using PlikShare.Boxes.Get;
using PlikShare.Boxes.Id;
using PlikShare.Mcp.Boxes.Members.List.Contracts;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.Id;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.Mcp.Boxes.Members.List;

/// <summary>
/// The reusable core of list_box_members: re-validates the agent's workspace access, resolves the box
/// within that workspace and lists its members, writing the audit entry. Called directly by the tool when
/// no approval is required, and by the execute flow once a human has approved the operation. The read is
/// idempotent, so the execute flow simply re-lists.
/// </summary>
public class ListBoxMembersAgentOperation(
    WorkspaceAgentMembershipCache workspaceAgentMembershipCache,
    BoxCache boxCache,
    GetBoxQuery getBoxQuery,
    AuditLogService auditLogService)
{
    public async Task<ListBoxMembersResponseDto> Execute(
        HttpContext httpContext,
        ListBoxMembersParams parameters,
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

        var details = getBoxQuery.Execute(
            box: box,
            workspaceEncryptionSession: null);

        await auditLogService.Log(
            Audit.Agent.BoxMembersListedEntry(
                actor: httpContext.GetAuditLogActorContext(),
                workspaceExternalId: parameters.WorkspaceExternalId,
                boxExternalId: parameters.BoxExternalId,
                count: details.Members.Count),
            cancellationToken);

        return new ListBoxMembersResponseDto
        {
            Members = details.Members
                .Select(member => new ListBoxMembersResponseDto.BoxMemberDto
                {
                    MemberExternalId = member.MemberExternalId,
                    Email = member.MemberEmail,
                    InviterEmail = member.InviterEmail,
                    InvitationAccepted = member.WasInvitationAccepted,
                    Permissions = new ListBoxMembersResponseDto.BoxMemberPermissionsDto
                    {
                        AllowDownload = member.Permissions.AllowDownload,
                        AllowUpload = member.Permissions.AllowUpload,
                        AllowList = member.Permissions.AllowList,
                        AllowDeleteFile = member.Permissions.AllowDeleteFile,
                        AllowRenameFile = member.Permissions.AllowRenameFile,
                        AllowMoveItems = member.Permissions.AllowMoveItems,
                        AllowCreateFolder = member.Permissions.AllowCreateFolder,
                        AllowRenameFolder = member.Permissions.AllowRenameFolder,
                        AllowDeleteFolder = member.Permissions.AllowDeleteFolder
                    }
                })
                .ToList()
        };
    }
}
