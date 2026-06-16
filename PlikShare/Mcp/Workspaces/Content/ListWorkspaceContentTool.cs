using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using PlikShare.Agents.Middleware;
using PlikShare.AuditLog;
using PlikShare.Folders.Id;
using PlikShare.Mcp.Workspaces.Content.Contracts;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.Id;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.Mcp.Workspaces.Content;

[McpServerToolType]
public class ListWorkspaceContentTool
{
    [McpServerTool(Name = "list_workspace_content")]
    [Description("Lists the folders and files inside a workspace the agent can access. " +
                 "Omit folderExternalId to list the workspace root. Returns a single entries[] list " +
                 "where each entry has a 'type' of 'folder' or 'file'. Folders are returned before files. " +
                 "Use the returned nextCursor to fetch the next page (with hasMore=true), reusing the same " +
                 "workspace, folder and type.")]
    public static async Task<ListWorkspaceContentResponseDto> Execute(
        IHttpContextAccessor httpContextAccessor,
        WorkspaceAgentMembershipCache workspaceAgentMembershipCache,
        GetWorkspaceContentForAgentQuery getWorkspaceContentForAgentQuery,
        AuditLogService auditLogService,
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

        var membership = await httpContext.GetWorkspaceAgentMembershipDetails(
            workspaceAgentMembershipCache,
            WorkspaceExtId.Parse(workspaceExternalId),
            cancellationToken);

        var workspace = membership.Workspace;
        workspace.ThrowIfFullyEncrypted();

        var typeFilter = ParseTypeFilter(type);

        FolderExtId? folderId = string.IsNullOrWhiteSpace(folderExternalId)
            ? null
            : FolderExtId.Parse(folderExternalId);

        var parsedCursor = ParseCursor(
            cursor,
            typeFilter);

        var resolvedLimit = Math.Clamp(limit ?? 200, 1, 1000);

        var result = getWorkspaceContentForAgentQuery.Execute(
            workspace: workspace,
            folderExternalId: folderId,
            typeFilter: typeFilter,
            cursor: parsedCursor,
            limit: resolvedLimit);

        if (result.Code == GetWorkspaceContentForAgentQuery.ResultCode.FolderNotFound)
            throw new McpException(
                $"Folder '{folderExternalId}' was not found in workspace '{workspaceExternalId}'.");

        await auditLogService.Log(
            Audit.Agent.WorkspaceContentListedEntry(
                actor: httpContext.GetAuditLogActorContext(),
                workspaceExternalId: workspaceExternalId,
                folderExternalId: folderId?.Value,
                count: result.Entries.Count),
            cancellationToken);

        return new ListWorkspaceContentResponseDto
        {
            Path = result.Path,
            Entries = result.Entries,
            NextCursor = result.NextCursor?.Encode(),
            HasMore = result.HasMore
        };
    }

    private static WorkspaceContentTypeFilter ParseTypeFilter(
        string? type)
    {
        if (string.IsNullOrWhiteSpace(type))
            return WorkspaceContentTypeFilter.All;

        return type.Trim().ToLowerInvariant() switch
        {
            "all" => WorkspaceContentTypeFilter.All,
            "folder" => WorkspaceContentTypeFilter.Folder,
            "file" => WorkspaceContentTypeFilter.File,
            _ => throw new McpException(
                $"Invalid type '{type}'. Allowed values are \"all\", \"folder\" and \"file\".")
        };
    }

    private static WorkspaceContentCursor? ParseCursor(
        string? cursor,
        WorkspaceContentTypeFilter typeFilter)
    {
        if (string.IsNullOrWhiteSpace(cursor))
            return null;

        var parsed = WorkspaceContentCursor.TryDecode(cursor)
            ?? throw new McpException(
                $"Invalid cursor '{cursor}'. Pass back the nextCursor value from a previous call.");

        var mismatch =
            (typeFilter == WorkspaceContentTypeFilter.Folder && parsed.Phase != WorkspaceContentPhase.Folder)
            || (typeFilter == WorkspaceContentTypeFilter.File && parsed.Phase != WorkspaceContentPhase.File);

        if (mismatch)
            throw new McpException(
                "The cursor does not match the requested type filter. Reuse the cursor with the same type.");

        return parsed;
    }
}
