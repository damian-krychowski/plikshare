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

namespace PlikShare.Mcp.BoxAccess.MoveItems;

[McpServerToolType]
public class MoveBoxItemsTool
{
    [McpServerTool(Name = AgentToolNames.MoveBoxItems)]
    [Description("Moves files and folders into another folder inside a box the agent was granted access to. " +
                 "Leave destinationFolderExternalId empty to move items to the box root. All ids must live " +
                 "inside the box. If this tool requires approval the call returns status 'waits_for_approval' " +
                 "with an approvalRequestId - poll check_approvals and, once approved, call execute_operation " +
                 "to run it.")]
    public static async Task<AgentToolResponse> Execute(
        IHttpContextAccessor httpContextAccessor,
        BoxCache boxCache,
        AgentBoxAccessCache boxAccessCache,
        AgentBoxToolOverrideReader boxToolOverrideReader,
        MoveBoxItemsAgentOperation moveBoxItemsOperation,
        AgentOperationLedger operationLedger,
        AgentOperationsOptions operationsOptions,
        IClock clock,
        [Description("External id of the box.")]
        string boxExternalId,
        [Description("External ids of the folders to move. Pass an empty array if none.")]
        string[] folderExternalIds,
        [Description("External ids of the files to move. Pass an empty array if none.")]
        string[] fileExternalIds,
        [Description("Optional external id of the destination folder inside the box. Leave empty for the box root.")]
        string? destinationFolderExternalId = null,
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

        var parameters = new MoveBoxItemsParams
        {
            BoxExternalId = boxExternalId,
            FolderExternalIds = folderExternalIds,
            FileExternalIds = fileExternalIds,
            DestinationFolderExternalId = string.IsNullOrWhiteSpace(destinationFolderExternalId)
                ? null
                : destinationFolderExternalId
        };

        var definition = AgentToolCatalog.TryGet(AgentToolNames.MoveBoxItems)!;

        var boxOverride = boxToolOverrideReader.TryGet(
            agentId: agent.Id,
            boxId: box.Id,
            toolName: AgentToolNames.MoveBoxItems);

        var effective = AgentToolCatalog.Resolve(
            agent,
            definition,
            workspaceOverride: null,
            boxOverride: boxOverride);

        if (!effective.IsUsable)
            throw new McpException("The move_box_items tool is not enabled for this box.");

        if (effective.RequiresApproval)
        {
            var expiresAt = clock.UtcNow.AddHours(
                operationsOptions.ApprovalWindowHours);

            var operationId = await operationLedger.CreatePending(
                agentId: agent.Id,
                workspaceId: box.Workspace.Id,
                toolName: AgentToolNames.MoveBoxItems,
                paramsJson: Json.Serialize(parameters),
                expiresAt: expiresAt,
                cancellationToken: cancellationToken);

            return AgentToolResponse.WaitsForApproval(
                approvalRequestId: operationId.Value,
                expiresAt: expiresAt);
        }

        var result = await moveBoxItemsOperation.Execute(
            httpContext,
            parameters,
            cancellationToken);

        return AgentToolResponse.Executed(
            result);
    }
}
