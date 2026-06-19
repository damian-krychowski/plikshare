using Microsoft.AspNetCore.Http;
using ModelContextProtocol;
using PlikShare.AuditLog;
using PlikShare.Boxes.Cache;
using PlikShare.Boxes.Id;
using PlikShare.Boxes.Members.CreateInvitation;
using PlikShare.Core.CorrelationId;
using PlikShare.Mcp.Boxes.Members.Invite.Contracts;
using PlikShare.Users.Cache;
using PlikShare.Users.Entities;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.Id;
using PlikShare.Workspaces.Members.CountAll;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.Mcp.Boxes.Members.Invite;

/// <summary>
/// The reusable core of invite_box_members: re-validates the agent's workspace access, resolves the box
/// within that workspace and invites the given emails on behalf of the agent's owner (recorded as the
/// inviter), writing the audit entry. Invitees start with list-only permissions, widened separately via
/// update_box_member_permissions. Called directly by the tool when no approval is required, and by the
/// execute flow once a human has approved the operation.
/// </summary>
public class InviteBoxMembersAgentOperation(
    WorkspaceAgentMembershipCache workspaceAgentMembershipCache,
    BoxCache boxCache,
    CreateBoxMemberInvitationOperation createBoxMemberInvitationOperation,
    CountWorkspaceTotalTeamMembersQuery countWorkspaceTotalTeamMembersQuery,
    UserCache userCache,
    AuditLogService auditLogService)
{
    public async Task<InviteBoxMembersResponseDto> Execute(
        HttpContext httpContext,
        InviteBoxMembersParams parameters,
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

        var emails = NormalizeEmails(parameters.MemberEmails);

        var maxTeamMembers = workspace.MaxTeamMembers;

        if (maxTeamMembers is not null)
        {
            var currentTeamMembers = countWorkspaceTotalTeamMembersQuery.Execute(
                workspaceId: workspace.Id);

            if (currentTeamMembers.TotalCount + emails.Length > maxTeamMembers)
                throw new McpException(
                    $"Inviting {emails.Length} member(s) would exceed the workspace team member limit of {maxTeamMembers}.");
        }

        var inviter = await userCache.TryGetUser(
            membership.Agent.Owner.Id,
            cancellationToken)
            ?? throw new McpException("The agent owner could not be resolved as the inviter.");

        var result = await createBoxMemberInvitationOperation.Execute(
            box: box,
            inviter: inviter,
            memberEmails: emails.Select(email => new Email(email)),
            correlationId: httpContext.GetCorrelationId(),
            cancellationToken: cancellationToken);

        foreach (var member in result.Members)
            await userCache.InvalidateEntry(member.Id, cancellationToken);

        await auditLogService.LogWithFolderContext(
            folderExternalId: box.Folder?.ExternalId,
            buildEntry: folderRef => Audit.Box.MemberInvitedEntry(
                actor: httpContext.GetAuditLogActorContext(),
                workspace: workspace.ToAuditLogWorkspaceRef(),
                box: new Audit.BoxRef { ExternalId = box.ExternalId, Name = box.Name, Folder = folderRef },
                members: result.Members
                    .Select(m => m.ToAuditLogUserRef())
                    .ToList()),
            cancellationToken);

        return new InviteBoxMembersResponseDto
        {
            Members = result.Members
                .Select(m => new InviteBoxMembersResponseDto.InvitedMember
                {
                    ExternalId = m.ExternalId.Value,
                    Email = m.Email.Value
                })
                .ToList()
        };
    }

    private static string[] NormalizeEmails(string[]? memberEmails)
    {
        var emails = (memberEmails ?? [])
            .Select(email => (email ?? string.Empty).Trim())
            .Where(email => email.Length > 0)
            .ToArray();

        if (emails.Length == 0)
            throw new McpException("Provide at least one email in memberEmails.");

        foreach (var email in emails)
        {
            if (!email.Contains('@'))
                throw new McpException($"'{email}' is not a valid email address.");
        }

        return emails;
    }
}
