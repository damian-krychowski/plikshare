using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using PlikShare.Agents.Middleware;
using PlikShare.Agents.Operations;
using PlikShare.Agents.Tools;
using PlikShare.Core.Clock;
using PlikShare.Core.Utils;

namespace PlikShare.Mcp.Search;

[McpServerToolType]
public class SearchTool
{
    [McpServerTool(Name = AgentToolNames.Search)]
    [Description(
        "Searches for files and folders the agent can access, across one or more workspaces. Every list " +
        "filter follows the same rule: values inside one list are combined with OR, different filters are " +
        "combined with AND, and an empty or omitted list means that filter is not applied. For example " +
        "extensions=[\"jpg\",\"png\"] with nameContains=[\"invoice\"] matches items whose name contains " +
        "'invoice' AND whose extension is jpg OR png. Results are newest-first and paginated: when hasMore " +
        "is true, pass the returned nextCursor back as cursor to get the next page. " +
        "Scope: workspaceIds and folderIds are optional — omit both to search every workspace the agent can " +
        "access; ids the agent cannot access are silently ignored. excludeWorkspaceIds and excludeFolderIds " +
        "remove those workspaces or folder subtrees from the results. extensions, contentTypes and size filters " +
        "apply to files only; combining them with types=[\"folder\"] is rejected. createdAfter/createdBefore " +
        "(ISO 8601) and sizeMin/sizeMax are range bounds, not lists. Each result carries its workspaceExternalId " +
        "and parent folderExternalId so you know where it lives. If this tool requires approval the call " +
        "returns status 'waits_for_approval' with an approvalRequestId — poll check_approvals and, once " +
        "approved, call execute_operation to run it.")]
    public static async Task<AgentToolResponse> Execute(
        IHttpContextAccessor httpContextAccessor,
        SearchAgentOperation searchOperation,
        AgentSearchScopeResolver searchScopeResolver,
        AgentOperationLedger operationLedger,
        AgentOperationsOptions operationsOptions,
        IClock clock,
        [Description("Workspace external ids to search in. Empty/omitted = all workspaces the agent can access.")]
        string[]? workspaceIds = null,
        [Description("Folder external ids to scope the search to (their whole subtrees). Empty/omitted = no folder scoping.")]
        string[]? folderIds = null,
        [Description("Workspace external ids to exclude from the search. Empty/omitted = nothing excluded.")]
        string[]? excludeWorkspaceIds = null,
        [Description("Folder external ids to exclude (their whole subtrees, and the folders themselves). Empty/omitted = nothing excluded.")]
        string[]? excludeFolderIds = null,
        [Description("Item types to return: \"file\" and/or \"folder\". Empty/omitted = both.")]
        string[]? types = null,
        [Description("Substrings to match in the name (OR). Case-insensitive. Empty/omitted = no name filter.")]
        string[]? nameContains = null,
        [Description("File extensions to match (OR), files only, e.g. [\"pdf\",\"jpg\"]. With or without a leading dot.")]
        string[]? extensions = null,
        [Description("Content types to match (OR), files only. Each is exact (\"image/png\") or a type prefix (\"image/*\").")]
        string[]? contentTypes = null,
        [Description("Only items created at or after this ISO 8601 timestamp.")]
        string? createdAfter = null,
        [Description("Only items created at or before this ISO 8601 timestamp.")]
        string? createdBefore = null,
        [Description("Minimum file size in bytes (files only).")]
        long? sizeMin = null,
        [Description("Maximum file size in bytes (files only).")]
        long? sizeMax = null,
        [Description("Pagination cursor: the nextCursor from a previous call. Omit for the first page.")]
        string? cursor = null,
        [Description("Maximum number of results per page. Default 200, maximum 1000.")]
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        var httpContext = httpContextAccessor.HttpContext!;
        var agent = await httpContext.GetAgentContext();

        var parameters = new SearchParams
        {
            WorkspaceIds = workspaceIds,
            FolderIds = folderIds,
            ExcludeWorkspaceIds = excludeWorkspaceIds,
            ExcludeFolderIds = excludeFolderIds,
            Types = types,
            NameContains = nameContains,
            Extensions = extensions,
            ContentTypes = contentTypes,
            CreatedAfter = createdAfter,
            CreatedBefore = createdBefore,
            SizeMin = sizeMin,
            SizeMax = sizeMax,
            Cursor = cursor,
            Limit = limit
        };

        // search has no single workspace: a disabled workspace drops out of the scope (applied in the
        // operation) and approval is required if any in-scope workspace requires it.
        var scope = searchScopeResolver.Resolve(agent);

        if (!scope.AnyEnabled)
            throw new McpException("The search tool is not enabled for this agent.");

        if (scope.RequiresApproval)
        {
            var expiresAt = clock.UtcNow.AddHours(
                operationsOptions.ApprovalWindowHours);

            var operationId = await operationLedger.CreatePending(
                agentId: agent.Id,
                workspaceId: null,
                toolName: AgentToolNames.Search,
                paramsJson: Json.Serialize(parameters),
                expiresAt: expiresAt,
                cancellationToken: cancellationToken);

            return AgentToolResponse.WaitsForApproval(
                approvalRequestId: operationId.Value,
                expiresAt: expiresAt);
        }

        var result = await searchOperation.Execute(
            httpContext,
            parameters,
            cancellationToken);

        return AgentToolResponse.Executed(
            result);
    }
}
