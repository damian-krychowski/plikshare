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

namespace PlikShare.Mcp.BoxAccess.Delete;

[McpServerToolType]
public class DeleteBoxItemsTool
{
    [McpServerTool(Name = AgentToolNames.DeleteBoxItems)]
    [Description("Deletes files and/or folders inside a box the agent was granted access to, including whole " +
                 "folder trees. All ids must live inside the box. This is destructive, so it requires approval " +
                 "by default: the call returns status 'waits_for_approval' with an approvalRequestId - poll " +
                 "check_approvals and, once approved, call execute_operation to run it.")]
    public static async Task<AgentToolResponse> Execute(
        IHttpContextAccessor httpContextAccessor,
        BoxCache boxCache,
        AgentBoxAccessCache boxAccessCache,
        AgentBoxToolOverrideReader boxToolOverrideReader,
        DeleteBoxItemsAgentOperation deleteBoxItemsOperation,
        AgentOperationLedger operationLedger,
        AgentOperationsOptions operationsOptions,
        IClock clock,
        [Description("External id of the box.")]
        string boxExternalId,
        [Description("External ids of the files to delete. Pass an empty array if none.")]
        string[] fileExternalIds,
        [Description("External ids of the folders to delete (whole trees). Pass an empty array if none.")]
        string[] folderExternalIds,
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

        var parameters = new DeleteBoxItemsParams
        {
            BoxExternalId = boxExternalId,
            FileExternalIds = fileExternalIds,
            FolderExternalIds = folderExternalIds
        };

        var definition = AgentToolCatalog.TryGet(AgentToolNames.DeleteBoxItems)!;

        var boxOverride = boxToolOverrideReader.TryGet(
            agentId: agent.Id,
            boxId: box.Id,
            toolName: AgentToolNames.DeleteBoxItems);

        var effective = AgentToolCatalog.Resolve(
            agent,
            definition,
            workspaceOverride: null,
            boxOverride: boxOverride);

        if (!effective.IsUsable)
            throw new McpException("The delete_box_items tool is not enabled for this box.");

        if (effective.RequiresApproval)
        {
            var expiresAt = clock.UtcNow.AddHours(
                operationsOptions.ApprovalWindowHours);

            var operationId = await operationLedger.CreatePending(
                agentId: agent.Id,
                workspaceId: box.Workspace.Id,
                toolName: AgentToolNames.DeleteBoxItems,
                paramsJson: Json.Serialize(parameters),
                expiresAt: expiresAt,
                cancellationToken: cancellationToken);

            return AgentToolResponse.WaitsForApproval(
                approvalRequestId: operationId.Value,
                expiresAt: expiresAt);
        }

        var result = await deleteBoxItemsOperation.Execute(
            httpContext,
            parameters,
            cancellationToken);

        return AgentToolResponse.Executed(
            result);
    }
}
