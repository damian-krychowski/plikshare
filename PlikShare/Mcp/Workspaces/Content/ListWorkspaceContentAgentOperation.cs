using Microsoft.AspNetCore.Http;
using ModelContextProtocol;
using PlikShare.AuditLog;
using PlikShare.Folders.Id;
using PlikShare.Mcp.Workspaces.Content.Contracts;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.Id;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.Mcp.Workspaces.Content;

/// <summary>
/// The reusable core of list_workspace_content: re-validates the agent's workspace access and lists
/// the folders and files inside a workspace or folder, writing the audit entry. Called directly by the
/// tool when no approval is required, and by the execute flow once a human has approved the operation.
/// The read is idempotent, so the execute flow simply re-lists.
/// </summary>
public class ListWorkspaceContentAgentOperation(
    WorkspaceAgentMembershipCache workspaceAgentMembershipCache,
    GetWorkspaceContentForAgentQuery getWorkspaceContentForAgentQuery,
    AuditLogService auditLogService)
{
    public async Task<ListWorkspaceContentResponseDto> Execute(
        HttpContext httpContext,
        ListWorkspaceContentParams parameters,
        CancellationToken cancellationToken)
    {
        var membership = await httpContext.GetWorkspaceAgentMembershipDetails(
            workspaceAgentMembershipCache,
            WorkspaceExtId.Parse(parameters.WorkspaceExternalId),
            cancellationToken);

        var workspace = membership.Workspace;
        workspace.ThrowIfFullyEncrypted();

        var typeFilter = ParseTypeFilter(parameters.Type);

        FolderExtId? folderId = string.IsNullOrWhiteSpace(parameters.FolderExternalId)
            ? null
            : FolderExtId.Parse(parameters.FolderExternalId);

        var parsedCursor = ParseCursor(
            parameters.Cursor,
            typeFilter);

        var resolvedLimit = Math.Clamp(parameters.Limit ?? 200, 1, 1000);

        var result = getWorkspaceContentForAgentQuery.Execute(
            workspace: workspace,
            folderExternalId: folderId,
            typeFilter: typeFilter,
            cursor: parsedCursor,
            limit: resolvedLimit);

        if (result.Code == GetWorkspaceContentForAgentQuery.ResultCode.FolderNotFound)
            throw new McpException(
                $"Folder '{parameters.FolderExternalId}' was not found in workspace '{parameters.WorkspaceExternalId}'.");

        await auditLogService.Log(
            Audit.Agent.WorkspaceContentListedEntry(
                actor: httpContext.GetAuditLogActorContext(),
                workspaceExternalId: parameters.WorkspaceExternalId,
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
