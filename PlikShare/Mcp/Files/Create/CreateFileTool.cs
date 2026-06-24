using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using PlikShare.Agents.Middleware;
using PlikShare.Agents.Operations;
using PlikShare.Agents.Tools;
using PlikShare.Core.Clock;
using PlikShare.Core.Utils;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.Id;

namespace PlikShare.Mcp.Files.Create;

[McpServerToolType]
public class CreateFileTool
{
    [McpServerTool(Name = AgentToolNames.CreateFile)]
    [Description("Creates a new text file in a workspace the agent can access, from content the agent provides " +
                 "inline as UTF-8 text. Use it to save generated text artifacts (notes, reports, configs). " +
                 "The content must be UTF-8 text and at most 10 MB. The content type is derived from the file " +
                 "extension unless you pass contentType explicitly. If this tool requires approval the call " +
                 "returns status 'waits_for_approval' with an approvalRequestId - poll check_approvals and, " +
                 "once approved, call execute_operation to run it.")]
    public static async Task<AgentToolResponse> Execute(
        IHttpContextAccessor httpContextAccessor,
        WorkspaceAgentMembershipCache workspaceAgentMembershipCache,
        AgentWorkspaceToolOverrideReader workspaceToolOverrideReader,
        CreateFileAgentOperation createFileOperation,
        AgentOperationLedger operationLedger,
        AgentOperationsOptions operationsOptions,
        IClock clock,
        [Description("External id of the workspace.")]
        string workspaceExternalId,
        [Description("File name including its extension, e.g. \"report.md\".")]
        string name,
        [Description("The file content as UTF-8 text. At most 10 MB.")]
        string content,
        [Description("Optional folder id to create the file in; omit to create it at the workspace root.")]
        string? folderExternalId = null,
        [Description("Optional content type (e.g. text/markdown). If omitted it is derived from the extension.")]
        string? contentType = null,
        CancellationToken cancellationToken = default)
    {
        var httpContext = httpContextAccessor.HttpContext!;
        var agent = await httpContext.GetAgentContext();

        var membership = await httpContext.GetWorkspaceAgentMembershipDetails(
            workspaceAgentMembershipCache,
            WorkspaceExtId.Parse(workspaceExternalId),
            cancellationToken);

        var workspace = membership.Workspace;
        workspace.ThrowIfFullyEncrypted();

        var parameters = new CreateFileParams
        {
            WorkspaceExternalId = workspaceExternalId,
            Name = name,
            Content = content ?? string.Empty,
            FolderExternalId = string.IsNullOrWhiteSpace(folderExternalId)
                ? null
                : folderExternalId,
            ContentType = string.IsNullOrWhiteSpace(contentType)
                ? null
                : contentType
        };

        var definition = AgentToolCatalog.TryGet(AgentToolNames.CreateFile)!;

        var workspaceOverride = workspaceToolOverrideReader.TryGet(
            agentId: agent.Id,
            workspaceId: workspace.Id,
            toolName: AgentToolNames.CreateFile);

        var effective = AgentToolCatalog.Resolve(
            agent,
            definition,
            workspaceOverride);

        if (!effective.IsUsable)
            throw new McpException("The create_file tool is not enabled for this workspace.");

        if (effective.RequiresApproval)
        {
            var expiresAt = clock.UtcNow.AddHours(
                operationsOptions.ApprovalWindowHours);

            var operationId = await operationLedger.CreatePending(
                agentId: agent.Id,
                workspaceId: workspace.Id,
                toolName: AgentToolNames.CreateFile,
                paramsJson: Json.Serialize(parameters),
                expiresAt: expiresAt,
                cancellationToken: cancellationToken);

            return AgentToolResponse.WaitsForApproval(
                approvalRequestId: operationId.Value,
                expiresAt: expiresAt);
        }

        var result = await createFileOperation.Execute(
            httpContext,
            parameters,
            cancellationToken);

        return AgentToolResponse.Executed(
            result);
    }
}
