using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using PlikShare.Agents.Middleware;
using PlikShare.Agents.Operations;
using PlikShare.Agents.Tools;
using PlikShare.Core.Clock;
using PlikShare.Core.Utils;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.Id;

namespace PlikShare.Mcp.Boxes.Members.Invite;

[McpServerToolType]
public class InviteBoxMembersTool
{
    [McpServerTool(Name = AgentToolNames.InviteBoxMembers)]
    [Description("Invites one or more people by email to a box in a workspace the agent can access. Each " +
                 "invitee receives an email invitation and gains list-only access to the box once they " +
                 "accept; use update_box_member_permissions to grant download, upload and other permissions. " +
                 "Returns the invited members with their external ids. If this tool requires approval the " +
                 "call returns status 'waits_for_approval' with an approvalRequestId - poll check_approvals " +
                 "and, once approved, call execute_operation to run it.")]
    public static async Task<AgentToolResponse> Execute(
        IHttpContextAccessor httpContextAccessor,
        WorkspaceAgentMembershipCache workspaceAgentMembershipCache,
        AgentWorkspaceToolOverrideReader workspaceToolOverrideReader,
        InviteBoxMembersAgentOperation inviteBoxMembersOperation,
        AgentOperationLedger operationLedger,
        AgentOperationsOptions operationsOptions,
        IClock clock,
        [Description("External id of the workspace.")]
        string workspaceExternalId,
        [Description("External id of the box.")]
        string boxExternalId,
        [Description("Email addresses of the people to invite. At least one is required.")]
        string[] memberEmails,
        CancellationToken cancellationToken = default)
    {
        var httpContext = httpContextAccessor.HttpContext!;
        var agent = await httpContext.GetAgentContext();

        var membership = await httpContext.GetWorkspaceAgentMembershipDetails(
            workspaceAgentMembershipCache,
            WorkspaceExtId.Parse(workspaceExternalId),
            cancellationToken);

        var workspace = membership.Workspace;
        workspace.ThrowIfFullyEncrypted();

        var emails = (memberEmails ?? [])
            .Select(email => (email ?? string.Empty).Trim())
            .Where(email => email.Length > 0)
            .ToArray();

        if (emails.Length == 0)
            throw new McpException("Provide at least one email in memberEmails.");

        var parameters = new InviteBoxMembersParams
        {
            WorkspaceExternalId = workspaceExternalId,
            BoxExternalId = boxExternalId,
            MemberEmails = emails
        };

        var definition = AgentToolCatalog.TryGet(AgentToolNames.InviteBoxMembers)!;

        var workspaceOverride = workspaceToolOverrideReader.TryGet(
            agentId: agent.Id,
            workspaceId: workspace.Id,
            toolName: AgentToolNames.InviteBoxMembers);

        var effective = AgentToolCatalog.Resolve(
            agent,
            definition,
            workspaceOverride);

        if (!effective.IsUsable)
            throw new McpException("The invite_box_members tool is not enabled for this workspace.");

        if (effective.RequiresApproval)
        {
            var expiresAt = clock.UtcNow.AddHours(
                operationsOptions.ApprovalWindowHours);

            var operationId = await operationLedger.CreatePending(
                agentId: agent.Id,
                workspaceId: workspace.Id,
                toolName: AgentToolNames.InviteBoxMembers,
                paramsJson: Json.Serialize(parameters),
                expiresAt: expiresAt,
                cancellationToken: cancellationToken);

            return AgentToolResponse.WaitsForApproval(
                approvalRequestId: operationId.Value,
                expiresAt: expiresAt);
        }

        var result = await inviteBoxMembersOperation.Execute(
            httpContext,
            parameters,
            cancellationToken);

        return AgentToolResponse.Executed(
            result);
    }
}
