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

namespace PlikShare.Mcp.BoxAccess.CreateFile;

[McpServerToolType]
public class CreateBoxFileTool
{
    [McpServerTool(Name = AgentToolNames.CreateBoxFile)]
    [Description("Creates a new text file inside a box the agent was granted access to, storing the given " +
                 "UTF-8 content. Leave folderExternalId empty to create the file at the box root, or pass a " +
                 "folder inside the box. If this tool requires approval the call returns status " +
                 "'waits_for_approval' with an approvalRequestId - poll check_approvals and, once approved, " +
                 "call execute_operation to run it.")]
    public static async Task<AgentToolResponse> Execute(
        IHttpContextAccessor httpContextAccessor,
        BoxCache boxCache,
        AgentBoxAccessCache boxAccessCache,
        AgentBoxToolOverrideReader boxToolOverrideReader,
        CreateBoxFileAgentOperation createBoxFileOperation,
        AgentOperationLedger operationLedger,
        AgentOperationsOptions operationsOptions,
        IClock clock,
        [Description("External id of the box.")]
        string boxExternalId,
        [Description("Name of the new file, including its extension (e.g. notes.txt).")]
        string name,
        [Description("UTF-8 text content of the file. May be empty.")]
        string? content = null,
        [Description("Optional external id of the target folder inside the box. Leave empty for the box root.")]
        string? folderExternalId = null,
        [Description("Optional MIME content type. Inferred from the extension when omitted.")]
        string? contentType = null,
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

        var parameters = new CreateBoxFileParams
        {
            BoxExternalId = boxExternalId,
            Name = name,
            Content = content,
            FolderExternalId = string.IsNullOrWhiteSpace(folderExternalId)
                ? null
                : folderExternalId,
            ContentType = contentType
        };

        var definition = AgentToolCatalog.TryGet(AgentToolNames.CreateBoxFile)!;

        var boxOverride = boxToolOverrideReader.TryGet(
            agentId: agent.Id,
            boxId: box.Id,
            toolName: AgentToolNames.CreateBoxFile);

        var effective = AgentToolCatalog.Resolve(
            agent,
            definition,
            workspaceOverride: null,
            boxOverride: boxOverride);

        if (!effective.IsUsable)
            throw new McpException("The create_box_file tool is not enabled for this box.");

        if (effective.RequiresApproval)
        {
            var expiresAt = clock.UtcNow.AddHours(
                operationsOptions.ApprovalWindowHours);

            var operationId = await operationLedger.CreatePending(
                agentId: agent.Id,
                workspaceId: box.Workspace.Id,
                toolName: AgentToolNames.CreateBoxFile,
                paramsJson: Json.Serialize(parameters),
                expiresAt: expiresAt,
                cancellationToken: cancellationToken);

            return AgentToolResponse.WaitsForApproval(
                approvalRequestId: operationId.Value,
                expiresAt: expiresAt);
        }

        var result = await createBoxFileOperation.Execute(
            httpContext,
            parameters,
            cancellationToken);

        return AgentToolResponse.Executed(
            result);
    }
}
