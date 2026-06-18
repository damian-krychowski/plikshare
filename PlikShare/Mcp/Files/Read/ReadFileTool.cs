using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using PlikShare.Agents.Middleware;
using PlikShare.Agents.Operations;
using PlikShare.Agents.Tools;
using PlikShare.Core.Clock;
using PlikShare.Core.Utils;

namespace PlikShare.Mcp.Files.Read;

[McpServerToolType]
public class ReadFileTool
{
    [McpServerTool(Name = AgentToolNames.ReadFile)]
    [Description("Reads the text content of a file by its external id, decoded as UTF-8. The file is resolved " +
                 "across all workspaces the agent can access; if the agent has no access it is reported as not " +
                 "found. Only text files are returned — binary files (images, video, PDF, archives) are " +
                 "rejected with a clear error. Large files are read in pages: pass the returned nextOffset back " +
                 "as offset while hasMore is true to read the rest. If this tool requires approval the call " +
                 "returns status 'waits_for_approval' with an approvalRequestId — poll check_approvals and, " +
                 "once approved, call execute_operation to run it.")]
    public static async Task<AgentToolResponse> Execute(
        IHttpContextAccessor httpContextAccessor,
        AgentFileWorkspaceLocator fileWorkspaceLocator,
        ReadFileAgentOperation readFileOperation,
        AgentOperationLedger operationLedger,
        AgentOperationsOptions operationsOptions,
        IClock clock,
        [Description("External id of the file to read.")]
        string fileExternalId,
        [Description("Byte offset to start reading from. Use 0 for the beginning, or the nextOffset from a previous call to continue.")]
        long offset = 0,
        [Description("Maximum number of bytes to read in this call. Default 65536, minimum 1024, maximum 262144.")]
        int? maxBytes = null,
        CancellationToken cancellationToken = default)
    {
        var httpContext = httpContextAccessor.HttpContext!;
        var agent = await httpContext.GetAgentContext();

        var parameters = new ReadFileParams
        {
            FileExternalId = fileExternalId,
            Offset = offset,
            MaxBytes = maxBytes
        };

        var definition = AgentToolCatalog.TryGet(AgentToolNames.ReadFile)!;

        // The file's workspace isn't an argument — resolve it so a per-workspace override applies.
        var located = await fileWorkspaceLocator.Locate(
            agent,
            fileExternalId,
            AgentToolNames.ReadFile,
            cancellationToken);

        var effective = AgentToolCatalog.Resolve(
            agent,
            definition,
            located.Override);

        if (!effective.IsUsable)
            throw new McpException("The read_file tool is not enabled.");

        if (effective.RequiresApproval)
        {
            var expiresAt = clock.UtcNow.AddHours(
                operationsOptions.ApprovalWindowHours);

            var operationId = await operationLedger.CreatePending(
                agentId: agent.Id,
                workspaceId: located.WorkspaceId,
                toolName: AgentToolNames.ReadFile,
                paramsJson: Json.Serialize(parameters),
                expiresAt: expiresAt,
                cancellationToken: cancellationToken);

            return AgentToolResponse.WaitsForApproval(
                approvalRequestId: operationId.Value,
                expiresAt: expiresAt);
        }

        var result = await readFileOperation.Execute(
            httpContext,
            parameters,
            cancellationToken);

        return AgentToolResponse.Executed(
            result);
    }
}
