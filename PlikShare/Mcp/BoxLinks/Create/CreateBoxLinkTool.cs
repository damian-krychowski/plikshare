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

namespace PlikShare.Mcp.BoxLinks.Create;

[McpServerToolType]
public class CreateBoxLinkTool
{
    [McpServerTool(Name = AgentToolNames.CreateBoxLink)]
    [Description("Creates a public link to a box in a workspace the agent can access. Anyone with the link " +
                 "and its access code can interact with the box according to the link's permissions (by " +
                 "default: list only). Use update_box_link afterwards to grant download, upload and other " +
                 "permissions. Returns the link's external id and access code. If this tool requires approval " +
                 "the call returns status 'waits_for_approval' with an approvalRequestId - poll check_approvals " +
                 "and, once approved, call execute_operation to run it.")]
    public static async Task<AgentToolResponse> Execute(
        IHttpContextAccessor httpContextAccessor,
        WorkspaceAgentMembershipCache workspaceAgentMembershipCache,
        AgentWorkspaceToolOverrideReader workspaceToolOverrideReader,
        CreateBoxLinkAgentOperation createBoxLinkOperation,
        AgentOperationLedger operationLedger,
        AgentOperationsOptions operationsOptions,
        IClock clock,
        [Description("External id of the workspace.")]
        string workspaceExternalId,
        [Description("External id of the box to create a link for.")]
        string boxExternalId,
        [Description("Name for the box link.")]
        string name,
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

        var trimmedName = (name ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(trimmedName))
            throw new McpException("A name for the box link is required.");

        var parameters = new CreateBoxLinkParams
        {
            WorkspaceExternalId = workspaceExternalId,
            BoxExternalId = boxExternalId,
            Name = trimmedName
        };

        var definition = AgentToolCatalog.TryGet(AgentToolNames.CreateBoxLink)!;

        var workspaceOverride = workspaceToolOverrideReader.TryGet(
            agentId: agent.Id,
            workspaceId: workspace.Id,
            toolName: AgentToolNames.CreateBoxLink);

        var effective = AgentToolCatalog.Resolve(
            agent,
            definition,
            workspaceOverride);

        if (!effective.IsUsable)
            throw new McpException("The create_box_link tool is not enabled for this workspace.");

        if (effective.RequiresApproval)
        {
            var expiresAt = clock.UtcNow.AddHours(
                operationsOptions.ApprovalWindowHours);

            var operationId = await operationLedger.CreatePending(
                agentId: agent.Id,
                workspaceId: workspace.Id,
                toolName: AgentToolNames.CreateBoxLink,
                paramsJson: Json.Serialize(parameters),
                expiresAt: expiresAt,
                cancellationToken: cancellationToken);

            return AgentToolResponse.WaitsForApproval(
                approvalRequestId: operationId.Value,
                expiresAt: expiresAt);
        }

        var result = await createBoxLinkOperation.Execute(
            httpContext,
            parameters,
            cancellationToken);

        return AgentToolResponse.Executed(
            result);
    }
}
