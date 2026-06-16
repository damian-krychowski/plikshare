using System.ComponentModel;
using System.Globalization;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using PlikShare.Agents.Middleware;
using PlikShare.AuditLog;
using PlikShare.Mcp.Search.Contracts;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.Mcp.Search;

[McpServerToolType]
public class SearchTool
{
    private const int DefaultLimit = 200;
    private const int MaxLimit = 1000;

    [McpServerTool(Name = "search")]
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
        "and parent folderExternalId so you know where it lives.")]
    public static async Task<SearchResponseDto> Execute(
        IHttpContextAccessor httpContextAccessor,
        SearchForAgentQuery searchForAgentQuery,
        AuditLogService auditLogService,
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

        var workspaceExternalIds = CleanList(workspaceIds);
        var folderExternalIds = CleanList(folderIds);
        var excludeWorkspaceExternalIds = CleanList(excludeWorkspaceIds);
        var excludeFolderExternalIds = CleanList(excludeFolderIds);
        var nameContainsList = CleanList(nameContains);

        var typesList = CleanList(types)
            .Select(t => t.ToLowerInvariant())
            .Distinct()
            .ToList();

        foreach (var type in typesList)
            if (type is not ("file" or "folder"))
                throw new McpException($"Invalid type '{type}'. Allowed types are 'file' and 'folder'.");

        var includeFiles = typesList.Count == 0 || typesList.Contains("file");
        var includeFolders = typesList.Count == 0 || typesList.Contains("folder");

        var extensionsList = CleanList(extensions)
            .Select(e => e.TrimStart('.').ToLowerInvariant())
            .Where(e => e.Length > 0)
            .Distinct()
            .ToList();

        var contentTypesList = CleanList(contentTypes)
            .Select(c => c.ToLowerInvariant())
            .Distinct()
            .ToList();

        var contentTypesPrefix = contentTypesList
            .Where(c => c.EndsWith("/*", StringComparison.Ordinal))
            .Select(c => c[..^1])
            .ToList();

        var contentTypesExact = contentTypesList
            .Where(c => !c.EndsWith("/*", StringComparison.Ordinal))
            .ToList();

        var hasFileOnlyFilter =
            extensionsList.Count > 0
            || contentTypesList.Count > 0
            || sizeMin.HasValue
            || sizeMax.HasValue;

        if (hasFileOnlyFilter && !includeFiles)
            throw new McpException(
                "extensions, contentTypes and size filters apply to files only — remove them or include " +
                "'file' in types.");

        if (hasFileOnlyFilter)
            includeFolders = false;

        var createdAfterValue = ParseTimestamp(createdAfter, nameof(createdAfter));
        var createdBeforeValue = ParseTimestamp(createdBefore, nameof(createdBefore));

        if (createdAfterValue is { } after && createdBeforeValue is { } before && after > before)
            throw new McpException("createdAfter must not be later than createdBefore.");

        if (sizeMin is < 0)
            throw new McpException("sizeMin must be zero or greater.");

        if (sizeMax is < 0)
            throw new McpException("sizeMax must be zero or greater.");

        if (sizeMin is { } min && sizeMax is { } max && min > max)
            throw new McpException("sizeMin must not be greater than sizeMax.");

        var effectiveLimit = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);

        SearchCursor? decodedCursor = null;

        if (!string.IsNullOrWhiteSpace(cursor))
        {
            decodedCursor = SearchCursor.TryDecode(cursor);

            if (decodedCursor is null)
                throw new McpException("Invalid cursor.");
        }

        var result = searchForAgentQuery.Execute(
            agent: agent,
            filters: new SearchForAgentQuery.Filters(
                WorkspaceExternalIds: workspaceExternalIds,
                FolderExternalIds: folderExternalIds,
                ExcludeWorkspaceExternalIds: excludeWorkspaceExternalIds,
                ExcludeFolderExternalIds: excludeFolderExternalIds,
                IncludeFolders: includeFolders,
                IncludeFiles: includeFiles,
                NameContains: nameContainsList,
                Extensions: extensionsList,
                ContentTypesExact: contentTypesExact,
                ContentTypesPrefix: contentTypesPrefix,
                CreatedAfter: createdAfterValue,
                CreatedBefore: createdBeforeValue,
                SizeMin: sizeMin,
                SizeMax: sizeMax),
            cursor: decodedCursor,
            limit: effectiveLimit);

        await auditLogService.Log(
            Audit.Agent.SearchPerformedEntry(
                actor: httpContext.GetAuditLogActorContext(),
                workspaceExternalIds: workspaceExternalIds,
                folderExternalIds: folderExternalIds,
                excludeWorkspaceExternalIds: excludeWorkspaceExternalIds,
                excludeFolderExternalIds: excludeFolderExternalIds,
                types: typesList,
                nameContains: nameContainsList,
                extensions: extensionsList,
                contentTypes: contentTypesList,
                createdAfter: createdAfterValue,
                createdBefore: createdBeforeValue,
                sizeMin: sizeMin,
                sizeMax: sizeMax,
                resultCount: result.Entries.Count),
            cancellationToken);

        return new SearchResponseDto
        {
            Entries = result.Entries,
            NextCursor = result.NextCursor?.Encode(),
            HasMore = result.HasMore
        };
    }

    private static List<string> CleanList(string[]? values)
    {
        if (values is null)
            return [];

        return values
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v.Trim())
            .Distinct()
            .ToList();
    }

    private static DateTimeOffset? ParseTimestamp(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (!DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
            throw new McpException($"Invalid {fieldName} '{value}'. Use an ISO 8601 timestamp.");

        return parsed.ToUniversalTime();
    }
}
