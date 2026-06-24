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

namespace PlikShare.Mcp.Boxes.Create;

[McpServerToolType]
public class CreateBoxTool
{
    [McpServerTool(Name = AgentToolNames.CreateBox)]
    [Description("Creates a box in a workspace the agent can access. A box is a curated view of a folder " +
                 "that can be shared with people (members) or via public box links. Provide the workspace " +
                 "id, a name and the external id of the folder the box exposes. Returns the box's external " +
                 "id. If this tool requires approval the call returns status 'waits_for_approval' with an " +
                 "approvalRequestId - poll check_approvals and, once approved, call execute_operation to run it.")]
    public static async Task<AgentToolResponse> Execute(
        IHttpContextAccessor httpContextAccessor,
        WorkspaceAgentMembershipCache workspaceAgentMembershipCache,
        AgentWorkspaceToolOverrideReader workspaceToolOverrideReader,
        CreateBoxAgentOperation createBoxOperation,
        AgentOperationLedger operationLedger,
        AgentOperationsOptions operationsOptions,
        IClock clock,
        [Description("External id of the workspace.")]
        string workspaceExternalId,
        [Description("Name for the box.")]
        string name,
        [Description("External id of the folder the box exposes.")]
        string folderExternalId,
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
            throw new McpException("A name for the box is required.");

        if (string.IsNullOrWhiteSpace(folderExternalId))
            throw new McpException("A folderExternalId is required.");

        var parameters = new CreateBoxParams
        {
            WorkspaceExternalId = workspaceExternalId,
            Name = trimmedName,
            FolderExternalId = folderExternalId
        };

        var definition = AgentToolCatalog.TryGet(AgentToolNames.CreateBox)!;

        var workspaceOverride = workspaceToolOverrideReader.TryGet(
            agentId: agent.Id,
            workspaceId: workspace.Id,
            toolName: AgentToolNames.CreateBox);

        var effective = AgentToolCatalog.Resolve(
            agent,
            definition,
            workspaceOverride);

        if (!effective.IsUsable)
            throw new McpException("The create_box tool is not enabled for this workspace.");

        if (effective.RequiresApproval)
        {
            var expiresAt = clock.UtcNow.AddHours(
                operationsOptions.ApprovalWindowHours);

            var operationId = await operationLedger.CreatePending(
                agentId: agent.Id,
                workspaceId: workspace.Id,
                toolName: AgentToolNames.CreateBox,
                paramsJson: Json.Serialize(parameters),
                expiresAt: expiresAt,
                cancellationToken: cancellationToken);

            return AgentToolResponse.WaitsForApproval(
                approvalRequestId: operationId.Value,
                expiresAt: expiresAt);
        }

        var result = await createBoxOperation.Execute(
            httpContext,
            parameters,
            cancellationToken);

        return AgentToolResponse.Executed(
            result);
    }
}
