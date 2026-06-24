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

namespace PlikShare.Mcp.BoxAccess.RenameFolder;

[McpServerToolType]
public class RenameBoxFolderTool
{
    [McpServerTool(Name = AgentToolNames.RenameBoxFolder)]
    [Description("Renames a folder inside a box the agent was granted access to. The folder must live " +
                 "inside the box. If this tool requires approval the call returns status 'waits_for_approval' " +
                 "with an approvalRequestId - poll check_approvals and, once approved, call execute_operation " +
                 "to run it.")]
    public static async Task<AgentToolResponse> Execute(
        IHttpContextAccessor httpContextAccessor,
        BoxCache boxCache,
        AgentBoxAccessCache boxAccessCache,
        AgentBoxToolOverrideReader boxToolOverrideReader,
        RenameBoxFolderAgentOperation renameBoxFolderOperation,
        AgentOperationLedger operationLedger,
        AgentOperationsOptions operationsOptions,
        IClock clock,
        [Description("External id of the box.")]
        string boxExternalId,
        [Description("External id of the folder to rename.")]
        string folderExternalId,
        [Description("New name for the folder.")]
        string name,
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

        var parameters = new RenameBoxFolderParams
        {
            BoxExternalId = boxExternalId,
            FolderExternalId = folderExternalId,
            Name = name
        };

        var definition = AgentToolCatalog.TryGet(AgentToolNames.RenameBoxFolder)!;

        var boxOverride = boxToolOverrideReader.TryGet(
            agentId: agent.Id,
            boxId: box.Id,
            toolName: AgentToolNames.RenameBoxFolder);

        var effective = AgentToolCatalog.Resolve(
            agent,
            definition,
            workspaceOverride: null,
            boxOverride: boxOverride);

        if (!effective.IsUsable)
            throw new McpException("The rename_box_folder tool is not enabled for this box.");

        if (effective.RequiresApproval)
        {
            var expiresAt = clock.UtcNow.AddHours(
                operationsOptions.ApprovalWindowHours);

            var operationId = await operationLedger.CreatePending(
                agentId: agent.Id,
                workspaceId: box.Workspace.Id,
                toolName: AgentToolNames.RenameBoxFolder,
                paramsJson: Json.Serialize(parameters),
                expiresAt: expiresAt,
                cancellationToken: cancellationToken);

            return AgentToolResponse.WaitsForApproval(
                approvalRequestId: operationId.Value,
                expiresAt: expiresAt);
        }

        var result = await renameBoxFolderOperation.Execute(
            httpContext,
            parameters,
            cancellationToken);

        return AgentToolResponse.Executed(
            result);
    }
}
