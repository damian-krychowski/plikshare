using Microsoft.AspNetCore.Http;
using ModelContextProtocol;
using PlikShare.AuditLog;
using PlikShare.Core.CorrelationId;
using PlikShare.Mcp.Workspaces.Members.Invite.Contracts;
using PlikShare.Users.Cache;
using PlikShare.Users.Entities;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.Id;
using PlikShare.Workspaces.Members.CountAll;
using PlikShare.Workspaces.Members.CreateInvitation;
using PlikShare.Workspaces.Permissions;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.Mcp.Workspaces.Members.Invite;

/// <summary>
/// The reusable core of invite_workspace_members: re-validates the agent's workspace access and invites
/// the given emails on behalf of the agent's owner (who is recorded as the inviter), writing the audit
/// entry. Called directly by the tool when no approval is required, and by the execute flow once a human
/// has approved the operation. Agents only ever act on non-full-encryption workspaces, so no owner
/// encryption session is needed.
/// </summary>
public class InviteWorkspaceMembersAgentOperation(
    WorkspaceAgentMembershipCache workspaceAgentMembershipCache,
    CreateWorkspaceMemberInvitationOperation createWorkspaceMemberInvitationOperation,
    CountWorkspaceTotalTeamMembersQuery countWorkspaceTotalTeamMembersQuery,
    UserCache userCache,
    AuditLogService auditLogService)
{
    public async Task<InviteWorkspaceMembersResponseDto> Execute(
        HttpContext httpContext,
        InviteWorkspaceMembersParams parameters,
        CancellationToken cancellationToken)
    {
        var membership = await httpContext.GetWorkspaceAgentMembershipDetails(
            workspaceAgentMembershipCache,
            WorkspaceExtId.Parse(parameters.WorkspaceExternalId),
            cancellationToken);

        var workspace = membership.Workspace;
        workspace.ThrowIfFullyEncrypted();

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

        var result = await createWorkspaceMemberInvitationOperation.Execute(
            workspace: workspace,
            inviter: inviter,
            memberEmails: emails.Select(email => new Email(email)),
            permission: new WorkspacePermissions(
                AllowShare: parameters.AllowShare),
            ownerSession: null,
            ephemeralDekLifetime: null,
            correlationId: httpContext.GetCorrelationId(),
            cancellationToken: cancellationToken);

        switch (result.Code)
        {
            case CreateWorkspaceMemberInvitationOperation.ResultCode.Ok:
                break;

            case CreateWorkspaceMemberInvitationOperation.ResultCode.EmailProviderNotConfigured:
                throw new McpException(
                    "The invitation could not be sent because no email provider is configured.");

            case CreateWorkspaceMemberInvitationOperation.ResultCode.EmailSendFailed:
                throw new McpException(
                    "The invitation could not be sent because email delivery failed.");

            default:
                throw new McpException($"Could not invite the workspace members: {result.Code}.");
        }

        foreach (var member in result.Members)
            await userCache.InvalidateEntry(member.Id, cancellationToken);

        await auditLogService.LogWithStorageContext(
            storageExternalId: workspace.Storage.ExternalId,
            buildEntry: storageRef => Audit.Workspace.MemberInvitedEntry(
                actor: httpContext.GetAuditLogActorContext(),
                storage: storageRef,
                workspace: workspace.ToAuditLogWorkspaceRef(),
                members: result.Members
                    .Select(m => m.ToAuditLogUserRef())
                    .ToList()),
            cancellationToken);

        return new InviteWorkspaceMembersResponseDto
        {
            Members = result.Members
                .Select(m => new InviteWorkspaceMembersResponseDto.InvitedMember
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
