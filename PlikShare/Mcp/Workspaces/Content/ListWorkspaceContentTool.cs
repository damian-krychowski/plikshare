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

namespace PlikShare.Mcp.Workspaces.Content;

[McpServerToolType]
public class ListWorkspaceContentTool
{
    [McpServerTool(Name = AgentToolNames.ListWorkspaceContent)]
    [Description("Lists the folders and files inside a workspace the agent can access. " +
                 "Omit folderExternalId to list the workspace root. Returns a single entries[] list " +
                 "where each entry has a 'type' of 'folder' or 'file'. Folders are returned before files. " +
                 "Use the returned nextCursor to fetch the next page (with hasMore=true), reusing the same " +
                 "workspace, folder and type. If this tool requires approval the call returns status " +
                 "'waits_for_approval' with an approvalRequestId — poll check_approvals and, once approved, " +
                 "call execute_operation to run it.")]
    public static async Task<AgentToolResponse> Execute(
        IHttpContextAccessor httpContextAccessor,
        WorkspaceAgentMembershipCache workspaceAgentMembershipCache,
        AgentWorkspaceToolOverrideReader workspaceToolOverrideReader,
        ListWorkspaceContentAgentOperation listWorkspaceContentOperation,
        AgentOperationLedger operationLedger,
        AgentOperationsOptions operationsOptions,
        IClock clock,
        [Description("External id of the workspace.")]
        string workspaceExternalId,
        [Description("Optional external id of the folder to list. Leave empty to list the workspace root.")]
        string? folderExternalId = null,
        [Description("Optional filter: \"all\" (default), \"folder\" or \"file\".")]
        string? type = null,
        [Description("Optional pagination cursor from a previous call's nextCursor. Use it with the same workspace, folder and type.")]
        string? cursor = null,
        [Description("Optional maximum number of entries to return. Default 200, maximum 1000.")]
        int? limit = null,
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

        var parameters = new ListWorkspaceContentParams
        {
            WorkspaceExternalId = workspaceExternalId,
            FolderExternalId = string.IsNullOrWhiteSpace(folderExternalId)
                ? null
                : folderExternalId,
            Type = type,
            Cursor = cursor,
            Limit = limit
        };

        var definition = AgentToolCatalog.TryGet(AgentToolNames.ListWorkspaceContent)!;

        var workspaceOverride = workspaceToolOverrideReader.TryGet(
            agentId: agent.Id,
            workspaceId: workspace.Id,
            toolName: AgentToolNames.ListWorkspaceContent);

        var effective = AgentToolCatalog.Resolve(
            agent,
            definition,
            workspaceOverride);

        if (!effective.IsUsable)
            throw new McpException("The list_workspace_content tool is not enabled for this workspace.");

        if (effective.RequiresApproval)
        {
            var expiresAt = clock.UtcNow.AddHours(
                operationsOptions.ApprovalWindowHours);

            var operationId = await operationLedger.CreatePending(
                agentId: agent.Id,
                workspaceId: workspace.Id,
                toolName: AgentToolNames.ListWorkspaceContent,
                paramsJson: Json.Serialize(parameters),
                expiresAt: expiresAt,
                cancellationToken: cancellationToken);

            return AgentToolResponse.WaitsForApproval(
                approvalRequestId: operationId.Value,
                expiresAt: expiresAt);
        }

        var result = await listWorkspaceContentOperation.Execute(
            httpContext,
            parameters,
            cancellationToken);

        return AgentToolResponse.Executed(
            result);
    }
}
