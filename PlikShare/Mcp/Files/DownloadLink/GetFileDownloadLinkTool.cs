using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using PlikShare.Agents.Middleware;
using PlikShare.Agents.Operations;
using PlikShare.Agents.Tools;
using PlikShare.Core.Clock;
using PlikShare.Core.Utils;

namespace PlikShare.Mcp.Files.DownloadLink;

[McpServerToolType]
public class GetFileDownloadLinkTool
{
    [McpServerTool(Name = AgentToolNames.GetFileDownloadLink)]
    [Description("Creates a short-lived, ready-to-download link for a file the agent can access, to hand to a " +
                 "user. Anyone with the link can download the file without logging in until it expires, so " +
                 "treat it as a capability and keep the expiry short. The file is resolved across all " +
                 "workspaces the agent can access; if the agent has no access it is reported as not found. If " +
                 "this tool requires approval the call returns status 'waits_for_approval' with an " +
                 "approvalRequestId — poll check_approvals and, once approved, call execute_operation to run it.")]
    public static async Task<AgentToolResponse> Execute(
        IHttpContextAccessor httpContextAccessor,
        AgentFileWorkspaceLocator fileWorkspaceLocator,
        GetFileDownloadLinkAgentOperation getFileDownloadLinkOperation,
        AgentOperationLedger operationLedger,
        AgentOperationsOptions operationsOptions,
        IClock clock,
        [Description("External id of the file to create a download link for.")]
        string fileExternalId,
        [Description("How long the link stays valid, in minutes. Default 15, minimum 1, maximum 1440 (24h).")]
        int? expiresInMinutes = null,
        CancellationToken cancellationToken = default)
    {
        var httpContext = httpContextAccessor.HttpContext!;
        var agent = await httpContext.GetAgentContext();

        var parameters = new GetFileDownloadLinkParams
        {
            FileExternalId = fileExternalId,
            ExpiresInMinutes = expiresInMinutes
        };

        var definition = AgentToolCatalog.TryGet(AgentToolNames.GetFileDownloadLink)!;

        // The file's workspace isn't an argument — resolve it so a per-workspace override applies.
        var located = await fileWorkspaceLocator.Locate(
            agent,
            fileExternalId,
            AgentToolNames.GetFileDownloadLink,
            cancellationToken);

        var effective = AgentToolCatalog.Resolve(
            agent,
            definition,
            located.Override);

        if (!effective.IsUsable)
            throw new McpException("The get_file_download_link tool is not enabled.");

        if (effective.RequiresApproval)
        {
            var expiresAt = clock.UtcNow.AddHours(
                operationsOptions.ApprovalWindowHours);

            var operationId = await operationLedger.CreatePending(
                agentId: agent.Id,
                workspaceId: located.WorkspaceId,
                toolName: AgentToolNames.GetFileDownloadLink,
                paramsJson: Json.Serialize(parameters),
                expiresAt: expiresAt,
                cancellationToken: cancellationToken);

            return AgentToolResponse.WaitsForApproval(
                approvalRequestId: operationId.Value,
                expiresAt: expiresAt);
        }

        var result = await getFileDownloadLinkOperation.Execute(
            httpContext,
            parameters,
            cancellationToken);

        return AgentToolResponse.Executed(
            result);
    }
}
