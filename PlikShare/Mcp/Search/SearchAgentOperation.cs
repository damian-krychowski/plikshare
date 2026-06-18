using System.Globalization;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol;
using PlikShare.Agents.Middleware;
using PlikShare.AuditLog;
using PlikShare.Mcp.Search.Contracts;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.Mcp.Search;

/// <summary>
/// The reusable core of search: validates the filters and searches for files and folders across the
/// agent's workspaces, writing the audit entry. Called directly by the tool when no approval is
/// required, and by the execute flow once a human has approved the operation. The read is idempotent,
/// so the execute flow simply re-searches.
/// </summary>
public class SearchAgentOperation(
    SearchForAgentQuery searchForAgentQuery,
    AgentSearchScopeResolver searchScopeResolver,
    AuditLogService auditLogService)
{
    private const int DefaultLimit = 200;
    private const int MaxLimit = 1000;

    public async Task<SearchResponseDto> Execute(
        HttpContext httpContext,
        SearchParams parameters,
        CancellationToken cancellationToken)
    {
        var agent = await httpContext.GetAgentContext();

        // Drop workspaces where search is disabled by a per-workspace override out of the scope, by
        // folding them into the exclude list the query already honours.
        var scope = searchScopeResolver.Resolve(agent);

        var workspaceExternalIds = CleanList(parameters.WorkspaceIds);
        var folderExternalIds = CleanList(parameters.FolderIds);
        var excludeWorkspaceExternalIds = CleanList(parameters.ExcludeWorkspaceIds)
            .Concat(scope.DisabledWorkspaceExternalIds)
            .Distinct()
            .ToList();
        var excludeFolderExternalIds = CleanList(parameters.ExcludeFolderIds);
        var nameContainsList = CleanList(parameters.NameContains);

        var typesList = CleanList(parameters.Types)
            .Select(t => t.ToLowerInvariant())
            .Distinct()
            .ToList();

        foreach (var type in typesList)
            if (type is not ("file" or "folder"))
                throw new McpException($"Invalid type '{type}'. Allowed types are 'file' and 'folder'.");

        var includeFiles = typesList.Count == 0 || typesList.Contains("file");
        var includeFolders = typesList.Count == 0 || typesList.Contains("folder");

        var extensionsList = CleanList(parameters.Extensions)
            .Select(e => e.TrimStart('.').ToLowerInvariant())
            .Where(e => e.Length > 0)
            .Distinct()
            .ToList();

        var contentTypesList = CleanList(parameters.ContentTypes)
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
            || parameters.SizeMin.HasValue
            || parameters.SizeMax.HasValue;

        if (hasFileOnlyFilter && !includeFiles)
            throw new McpException(
                "extensions, contentTypes and size filters apply to files only — remove them or include " +
                "'file' in types.");

        if (hasFileOnlyFilter)
            includeFolders = false;

        var createdAfterValue = ParseTimestamp(parameters.CreatedAfter, nameof(parameters.CreatedAfter));
        var createdBeforeValue = ParseTimestamp(parameters.CreatedBefore, nameof(parameters.CreatedBefore));

        if (createdAfterValue is { } after && createdBeforeValue is { } before && after > before)
            throw new McpException("createdAfter must not be later than createdBefore.");

        if (parameters.SizeMin is < 0)
            throw new McpException("sizeMin must be zero or greater.");

        if (parameters.SizeMax is < 0)
            throw new McpException("sizeMax must be zero or greater.");

        if (parameters.SizeMin is { } min && parameters.SizeMax is { } max && min > max)
            throw new McpException("sizeMin must not be greater than sizeMax.");

        var effectiveLimit = Math.Clamp(parameters.Limit ?? DefaultLimit, 1, MaxLimit);

        SearchCursor? decodedCursor = null;

        if (!string.IsNullOrWhiteSpace(parameters.Cursor))
        {
            decodedCursor = SearchCursor.TryDecode(parameters.Cursor);

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
                SizeMin: parameters.SizeMin,
                SizeMax: parameters.SizeMax),
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
                sizeMin: parameters.SizeMin,
                sizeMax: parameters.SizeMax,
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
