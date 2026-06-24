using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using PlikShare.Agents.BoxAccess;
using PlikShare.Agents.Middleware;
using PlikShare.Agents.Operations;
using PlikShare.Agents.Tools;
using PlikShare.Boxes.Cache;
using PlikShare.Boxes.Id;
using PlikShare.Core.Clock;
using PlikShare.Core.Utils;

namespace PlikShare.Mcp.BoxAccess.Content;

[McpServerToolType]
public class ListBoxContentTool
{
    [McpServerTool(Name = AgentToolNames.ListBoxContent)]
    [Description("Lists the folders and files inside a box the agent was granted access to. Leave " +
                 "folderExternalId empty to list the box's root, or pass a folder inside the box to list its " +
                 "children. If this tool requires approval the call returns status 'waits_for_approval' with " +
                 "an approvalRequestId - poll check_approvals and, once approved, call execute_operation to " +
                 "run it.")]
    public static async Task<AgentToolResponse> Execute(
        IHttpContextAccessor httpContextAccessor,
        BoxCache boxCache,
        AgentBoxAccessCache boxAccessCache,
        AgentBoxToolOverrideReader boxToolOverrideReader,
        ListBoxContentAgentOperation listBoxContentOperation,
        AgentOperationLedger operationLedger,
        AgentOperationsOptions operationsOptions,
        IClock clock,
        [Description("External id of the box.")]
        string boxExternalId,
        [Description("Optional external id of a folder inside the box. Leave empty for the box root.")]
        string? folderExternalId = null,
        CancellationToken cancellationToken = default)
    {
        var httpContext = httpContextAccessor.HttpContext!;
        var agent = await httpContext.GetAgentContext();

        var boxAccess = await httpContext.GetAgentBoxAccess(
            agent,
            boxCache,
            boxAccessCache,
            BoxExtId.Parse(boxExternalId),
            cancellationToken);

        var box = boxAccess.Box;

        var parameters = new ListBoxContentParams
        {
            BoxExternalId = boxExternalId,
            FolderExternalId = string.IsNullOrWhiteSpace(folderExternalId)
                ? null
                : folderExternalId
        };

        var definition = AgentToolCatalog.TryGet(AgentToolNames.ListBoxContent)!;

        var boxOverride = boxToolOverrideReader.TryGet(
            agentId: agent.Id,
            boxId: box.Id,
            toolName: AgentToolNames.ListBoxContent);

        var effective = AgentToolCatalog.Resolve(
            agent,
            definition,
            workspaceOverride: null,
            boxOverride: boxOverride);

        if (!effective.IsUsable)
            throw new McpException("The list_box_content tool is not enabled for this box.");

        if (effective.RequiresApproval)
        {
            var expiresAt = clock.UtcNow.AddHours(
                operationsOptions.ApprovalWindowHours);

            var operationId = await operationLedger.CreatePending(
                agentId: agent.Id,
                workspaceId: box.Workspace.Id,
                toolName: AgentToolNames.ListBoxContent,
                paramsJson: Json.Serialize(parameters),
                expiresAt: expiresAt,
                cancellationToken: cancellationToken);

            return AgentToolResponse.WaitsForApproval(
                approvalRequestId: operationId.Value,
                expiresAt: expiresAt);
        }

        var result = await listBoxContentOperation.Execute(
            httpContext,
            parameters,
            cancellationToken);

        return AgentToolResponse.Executed(
            result);
    }
}
