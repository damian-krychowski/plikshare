using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using PlikShare.Agents.Middleware;
using PlikShare.Agents.Operations;
using PlikShare.Agents.Tools;
using PlikShare.Core.Clock;
using PlikShare.Core.Utils;

namespace PlikShare.Mcp.Files.Get;

[McpServerToolType]
public class GetFileTool
{
    [McpServerTool(Name = AgentToolNames.GetFile)]
    [Description("Returns the details of a single file by its external id: name, extension, content type, " +
                 "size, creation time and the folder path it lives in. The file is resolved across all " +
                 "workspaces the agent can access; if the agent has no access to it, the tool reports it as " +
                 "not found without revealing whether it exists. If this tool requires approval the call " +
                 "returns status 'waits_for_approval' with an approvalRequestId — poll check_approvals and, " +
                 "once approved, call execute_operation to run it.")]
    public static async Task<AgentToolResponse> Execute(
        IHttpContextAccessor httpContextAccessor,
        AgentFileWorkspaceLocator fileWorkspaceLocator,
        GetFileAgentOperation getFileOperation,
        AgentOperationLedger operationLedger,
        AgentOperationsOptions operationsOptions,
        IClock clock,
        [Description("External id of the file.")]
        string fileExternalId,
        CancellationToken cancellationToken = default)
    {
        var httpContext = httpContextAccessor.HttpContext!;
        var agent = await httpContext.GetAgentContext();

        var parameters = new GetFileParams
        {
            FileExternalId = fileExternalId
        };

        var definition = AgentToolCatalog.TryGet(AgentToolNames.GetFile)!;

        // The file's workspace isn't an argument — resolve it so a per-workspace override applies.
        var located = await fileWorkspaceLocator.Locate(
            agent,
            fileExternalId,
            AgentToolNames.GetFile,
            cancellationToken);

        var effective = AgentToolCatalog.Resolve(
            agent,
            definition,
            located.Override);

        if (!effective.IsUsable)
            throw new McpException("The get_file tool is not enabled.");

        if (effective.RequiresApproval)
        {
            var expiresAt = clock.UtcNow.AddHours(
                operationsOptions.ApprovalWindowHours);

            var operationId = await operationLedger.CreatePending(
                agentId: agent.Id,
                workspaceId: located.WorkspaceId,
                toolName: AgentToolNames.GetFile,
                paramsJson: Json.Serialize(parameters),
                expiresAt: expiresAt,
                cancellationToken: cancellationToken);

            return AgentToolResponse.WaitsForApproval(
                approvalRequestId: operationId.Value,
                expiresAt: expiresAt);
        }

        var result = await getFileOperation.Execute(
            httpContext,
            parameters,
            cancellationToken);

        return AgentToolResponse.Executed(
            result);
    }
}
