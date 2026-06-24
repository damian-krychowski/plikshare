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

namespace PlikShare.Mcp.BoxAccess.ReadFile;

[McpServerToolType]
public class ReadBoxFileTool
{
    [McpServerTool(Name = AgentToolNames.ReadBoxFile)]
    [Description("Reads the UTF-8 text content of a file inside a box the agent was granted access to. The " +
                 "file must live inside the box and be a text file. Reads from offset for up to maxBytes bytes " +
                 "(default 64KB, max 256KB); use the returned nextOffset and hasMore to page through larger " +
                 "files. If this tool requires approval the call returns status 'waits_for_approval' with an " +
                 "approvalRequestId - poll check_approvals and, once approved, call execute_operation to run it.")]
    public static async Task<AgentToolResponse> Execute(
        IHttpContextAccessor httpContextAccessor,
        BoxCache boxCache,
        AgentBoxAccessCache boxAccessCache,
        AgentBoxToolOverrideReader boxToolOverrideReader,
        ReadBoxFileAgentOperation readBoxFileOperation,
        AgentOperationLedger operationLedger,
        AgentOperationsOptions operationsOptions,
        IClock clock,
        [Description("External id of the box.")]
        string boxExternalId,
        [Description("External id of the file to read.")]
        string fileExternalId,
        [Description("Byte offset to start reading from. Defaults to 0.")]
        long offset = 0,
        [Description("Maximum number of bytes to read (1024–262144). Defaults to 65536.")]
        int? maxBytes = null,
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

        var parameters = new ReadBoxFileParams
        {
            BoxExternalId = boxExternalId,
            FileExternalId = fileExternalId,
            Offset = offset,
            MaxBytes = maxBytes
        };

        var definition = AgentToolCatalog.TryGet(AgentToolNames.ReadBoxFile)!;

        var boxOverride = boxToolOverrideReader.TryGet(
            agentId: agent.Id,
            boxId: box.Id,
            toolName: AgentToolNames.ReadBoxFile);

        var effective = AgentToolCatalog.Resolve(
            agent,
            definition,
            workspaceOverride: null,
            boxOverride: boxOverride);

        if (!effective.IsUsable)
            throw new McpException("The read_box_file tool is not enabled for this box.");

        if (effective.RequiresApproval)
        {
            var expiresAt = clock.UtcNow.AddHours(
                operationsOptions.ApprovalWindowHours);

            var operationId = await operationLedger.CreatePending(
                agentId: agent.Id,
                workspaceId: box.Workspace.Id,
                toolName: AgentToolNames.ReadBoxFile,
                paramsJson: Json.Serialize(parameters),
                expiresAt: expiresAt,
                cancellationToken: cancellationToken);

            return AgentToolResponse.WaitsForApproval(
                approvalRequestId: operationId.Value,
                expiresAt: expiresAt);
        }

        var result = await readBoxFileOperation.Execute(
            httpContext,
            parameters,
            cancellationToken);

        return AgentToolResponse.Executed(
            result);
    }
}
