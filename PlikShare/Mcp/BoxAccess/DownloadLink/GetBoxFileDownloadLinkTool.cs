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

namespace PlikShare.Mcp.BoxAccess.DownloadLink;

[McpServerToolType]
public class GetBoxFileDownloadLinkTool
{
    [McpServerTool(Name = AgentToolNames.GetBoxFileDownloadLink)]
    [Description("Creates a short-lived link to download a single file from a box the agent was granted " +
                 "access to. The file must live inside the box. Pass expiresInMinutes to control the link's " +
                 "lifetime (1–1440, default 15). If this tool requires approval the call returns status " +
                 "'waits_for_approval' with an approvalRequestId - poll check_approvals and, once approved, " +
                 "call execute_operation to run it.")]
    public static async Task<AgentToolResponse> Execute(
        IHttpContextAccessor httpContextAccessor,
        BoxCache boxCache,
        AgentBoxAccessCache boxAccessCache,
        AgentBoxToolOverrideReader boxToolOverrideReader,
        GetBoxFileDownloadLinkAgentOperation getBoxFileDownloadLinkOperation,
        AgentOperationLedger operationLedger,
        AgentOperationsOptions operationsOptions,
        IClock clock,
        [Description("External id of the box.")]
        string boxExternalId,
        [Description("External id of the file to download.")]
        string fileExternalId,
        [Description("Optional link lifetime in minutes (1–1440). Defaults to 15.")]
        int? expiresInMinutes = null,
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

        var parameters = new GetBoxFileDownloadLinkParams
        {
            BoxExternalId = boxExternalId,
            FileExternalId = fileExternalId,
            ExpiresInMinutes = expiresInMinutes
        };

        var definition = AgentToolCatalog.TryGet(AgentToolNames.GetBoxFileDownloadLink)!;

        var boxOverride = boxToolOverrideReader.TryGet(
            agentId: agent.Id,
            boxId: box.Id,
            toolName: AgentToolNames.GetBoxFileDownloadLink);

        var effective = AgentToolCatalog.Resolve(
            agent,
            definition,
            workspaceOverride: null,
            boxOverride: boxOverride);

        if (!effective.IsUsable)
            throw new McpException("The get_box_file_download_link tool is not enabled for this box.");

        if (effective.RequiresApproval)
        {
            var expiresAt = clock.UtcNow.AddHours(
                operationsOptions.ApprovalWindowHours);

            var operationId = await operationLedger.CreatePending(
                agentId: agent.Id,
                workspaceId: box.Workspace.Id,
                toolName: AgentToolNames.GetBoxFileDownloadLink,
                paramsJson: Json.Serialize(parameters),
                expiresAt: expiresAt,
                cancellationToken: cancellationToken);

            return AgentToolResponse.WaitsForApproval(
                approvalRequestId: operationId.Value,
                expiresAt: expiresAt);
        }

        var result = await getBoxFileDownloadLinkOperation.Execute(
            httpContext,
            parameters,
            cancellationToken);

        return AgentToolResponse.Executed(
            result);
    }
}
