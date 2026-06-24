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

namespace PlikShare.Mcp.Workspaces.Members.Invite;

[McpServerToolType]
public class InviteWorkspaceMembersTool
{
    [McpServerTool(Name = AgentToolNames.InviteWorkspaceMembers)]
    [Description("Invites one or more people by email to a workspace the agent can access. Each invitee " +
                 "receives an email invitation and gains access once they accept. Set allowShare to let " +
                 "the invitees invite further members and manage permissions themselves (default false). " +
                 "Returns the invited members with their external ids. If this tool requires approval the " +
                 "call returns status 'waits_for_approval' with an approvalRequestId - poll check_approvals " +
                 "and, once approved, call execute_operation to run it.")]
    public static async Task<AgentToolResponse> Execute(
        IHttpContextAccessor httpContextAccessor,
        WorkspaceAgentMembershipCache workspaceAgentMembershipCache,
        AgentWorkspaceToolOverrideReader workspaceToolOverrideReader,
        InviteWorkspaceMembersAgentOperation inviteWorkspaceMembersOperation,
        AgentOperationLedger operationLedger,
        AgentOperationsOptions operationsOptions,
        IClock clock,
        [Description("External id of the workspace.")]
        string workspaceExternalId,
        [Description("Email addresses of the people to invite. At least one is required.")]
        string[] memberEmails,
        [Description("Whether the invitees may invite further members and manage permissions. Defaults to false.")]
        bool allowShare = false,
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

        var parameters = new InviteWorkspaceMembersParams
        {
            WorkspaceExternalId = workspaceExternalId,
            MemberEmails = emails,
            AllowShare = allowShare
        };

        var definition = AgentToolCatalog.TryGet(AgentToolNames.InviteWorkspaceMembers)!;

        var workspaceOverride = workspaceToolOverrideReader.TryGet(
            agentId: agent.Id,
            workspaceId: workspace.Id,
            toolName: AgentToolNames.InviteWorkspaceMembers);

        var effective = AgentToolCatalog.Resolve(
            agent,
            definition,
            workspaceOverride);

        if (!effective.IsUsable)
            throw new McpException("The invite_workspace_members tool is not enabled for this workspace.");

        if (effective.RequiresApproval)
        {
            var expiresAt = clock.UtcNow.AddHours(
                operationsOptions.ApprovalWindowHours);

            var operationId = await operationLedger.CreatePending(
                agentId: agent.Id,
                workspaceId: workspace.Id,
                toolName: AgentToolNames.InviteWorkspaceMembers,
                paramsJson: Json.Serialize(parameters),
                expiresAt: expiresAt,
                cancellationToken: cancellationToken);

            return AgentToolResponse.WaitsForApproval(
                approvalRequestId: operationId.Value,
                expiresAt: expiresAt);
        }

        var result = await inviteWorkspaceMembersOperation.Execute(
            httpContext,
            parameters,
            cancellationToken);

        return AgentToolResponse.Executed(
            result);
    }
}
