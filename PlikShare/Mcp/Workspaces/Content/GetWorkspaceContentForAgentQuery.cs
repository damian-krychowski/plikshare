using Microsoft.Data.Sqlite;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Folders.Id;
using PlikShare.Mcp.Workspaces.Content.Contracts;
using PlikShare.Workspaces.Cache;

namespace PlikShare.Mcp.Workspaces.Content;

public class GetWorkspaceContentForAgentQuery(PlikShareDb plikShareDb)
{
    public enum ResultCode
    {
        Ok,
        FolderNotFound
    }

    public readonly record struct Result(
        ResultCode Code,
        List<WorkspaceContentFolderDto> Path,
        List<WorkspaceContentEntryDto> Entries,
        WorkspaceContentCursor? NextCursor,
        bool HasMore);

    public Result Execute(
        WorkspaceContext workspace,
        FolderExtId? folderExternalId,
        WorkspaceContentTypeFilter typeFilter,
        WorkspaceContentCursor? cursor,
        int limit)
    {
        using var connection = plikShareDb.OpenConnection();

        var path = new List<WorkspaceContentFolderDto>();
        int? currentFolderId = null;

        if (folderExternalId is { } folderExtId)
        {
            var current = GetCurrentFolder(
                connection,
                workspace,
                folderExtId);

            if (current is null)
                return new Result(
                    Code: ResultCode.FolderNotFound,
                    Path: [],
                    Entries: [],
                    NextCursor: null,
                    HasMore: false);

            currentFolderId = current.Value.Id;

            path = GetAncestors(
                connection,
                workspace,
                current.Value.AncestorFolderIdsJson);

            path.Add(new WorkspaceContentFolderDto
            {
                ExternalId = current.Value.ExternalId,
                Name = current.Value.Name
            });
        }

        var (entries, nextCursor, hasMore) = BuildPage(
            connection,
            workspace,
            currentFolderId,
            typeFilter,
            cursor,
            limit);

        return new Result(
            Code: ResultCode.Ok,
            Path: path,
            Entries: entries,
            NextCursor: nextCursor,
            HasMore: hasMore);
    }

    private (List<WorkspaceContentEntryDto> Entries, WorkspaceContentCursor? NextCursor, bool HasMore) BuildPage(
        SqliteConnection connection,
        WorkspaceContext workspace,
        int? currentFolderId,
        WorkspaceContentTypeFilter typeFilter,
        WorkspaceContentCursor? cursor,
        int limit)
    {
        var entries = new List<WorkspaceContentEntryDto>();

        if (typeFilter == WorkspaceContentTypeFilter.Folder)
        {
            var afterId = cursor?.Phase == WorkspaceContentPhase.Folder ? cursor.LastId : 0;

            var folders = QueryFolders(
                connection,
                workspace,
                currentFolderId,
                afterId,
                limit + 1);

            var hasMore = folders.Count > limit;
            var page = folders.Take(limit).ToList();

            entries.AddRange(page.Select(x => x.Entry));

            return (
                entries,
                hasMore ? new WorkspaceContentCursor(WorkspaceContentPhase.Folder, page[^1].Id) : null,
                hasMore);
        }

        if (typeFilter == WorkspaceContentTypeFilter.File)
        {
            var afterId = cursor?.Phase == WorkspaceContentPhase.File ? cursor.LastId : 0;

            return FillFiles(
                connection,
                workspace,
                currentFolderId,
                entries,
                fileAfter: afterId,
                remaining: limit);
        }

        if (cursor is null || cursor.Phase == WorkspaceContentPhase.Folder)
        {
            var folderAfter = cursor?.LastId ?? 0;

            var folders = QueryFolders(
                connection,
                workspace,
                currentFolderId,
                folderAfter,
                limit + 1);

            if (folders.Count > limit)
            {
                var page = folders.Take(limit).ToList();
                entries.AddRange(page.Select(x => x.Entry));

                return (
                    entries,
                    new WorkspaceContentCursor(WorkspaceContentPhase.Folder, page[^1].Id),
                    true);
            }

            entries.AddRange(folders.Select(x => x.Entry));

            return FillFiles(
                connection,
                workspace,
                currentFolderId,
                entries,
                fileAfter: 0,
                remaining: limit - folders.Count);
        }

        return FillFiles(
            connection,
            workspace,
            currentFolderId,
            entries,
            fileAfter: cursor.LastId,
            remaining: limit);
    }

    private (List<WorkspaceContentEntryDto> Entries, WorkspaceContentCursor? NextCursor, bool HasMore) FillFiles(
        SqliteConnection connection,
        WorkspaceContext workspace,
        int? currentFolderId,
        List<WorkspaceContentEntryDto> entries,
        int fileAfter,
        int remaining)
    {
        var files = QueryFiles(
            connection,
            workspace,
            currentFolderId,
            fileAfter,
            remaining + 1);

        if (files.Count > remaining)
        {
            var page = files.Take(remaining).ToList();
            entries.AddRange(page.Select(x => x.Entry));

            var lastFileId = page.Count > 0 ? page[^1].Id : fileAfter;

            return (
                entries,
                new WorkspaceContentCursor(WorkspaceContentPhase.File, lastFileId),
                true);
        }

        entries.AddRange(files.Select(x => x.Entry));

        return (entries, null, false);
    }

    private static List<(int Id, WorkspaceContentEntryDto Entry)> QueryFolders(
        SqliteConnection connection,
        WorkspaceContext workspace,
        int? parentFolderId,
        int afterId,
        int take)
    {
        return connection
            .Cmd(
                sql: """
                     SELECT
                         fo_id,
                         fo_external_id,
                         fo_name,
                         fo_created_at
                     FROM fo_folders
                     WHERE
                         fo_workspace_id = $workspaceId
                         AND fo_is_being_deleted = FALSE
                         AND (
                             ($parentFolderId IS NULL AND fo_parent_folder_id IS NULL)
                             OR fo_parent_folder_id = $parentFolderId
                         )
                         AND fo_id > $afterId
                     ORDER BY fo_id ASC
                     LIMIT $take
                     """,
                readRowFunc: reader => (
                    Id: reader.GetInt32(0),
                    Entry: new WorkspaceContentEntryDto
                    {
                        Type = "folder",
                        ExternalId = reader.GetString(1),
                        Name = reader.DecodeEncryptableString(2, null),
                        Extension = null,
                        ContentType = null,
                        SizeInBytes = null,
                        CreatedAt = reader.GetDateTimeOffsetOrNull(3)?.UtcDateTime
                    }),
                name: "agent.workspace_content.folders")
            .WithParameter("$workspaceId", workspace.Id)
            .WithParameter("$parentFolderId", parentFolderId)
            .WithParameter("$afterId", afterId)
            .WithParameter("$take", take)
            .Execute();
    }

    private static List<(int Id, WorkspaceContentEntryDto Entry)> QueryFiles(
        SqliteConnection connection,
        WorkspaceContext workspace,
        int? folderId,
        int afterId,
        int take)
    {
        return connection
            .Cmd(
                sql: """
                     SELECT
                         fi_id,
                         fi_external_id,
                         fi_name,
                         fi_extension,
                         fi_content_type,
                         fi_size_in_bytes,
                         fi_created_at
                     FROM fi_files
                     WHERE
                         fi_workspace_id = $workspaceId
                         AND fi_parent_file_id IS NULL
                         AND fi_deleted_at IS NULL
                         AND fi_is_upload_completed = TRUE
                         AND (
                             ($folderId IS NULL AND fi_folder_id IS NULL)
                             OR fi_folder_id = $folderId
                         )
                         AND fi_id > $afterId
                     ORDER BY fi_id ASC
                     LIMIT $take
                     """,
                readRowFunc: reader => (
                    Id: reader.GetInt32(0),
                    Entry: new WorkspaceContentEntryDto
                    {
                        Type = "file",
                        ExternalId = reader.GetString(1),
                        Name = reader.DecodeEncryptableString(2, null),
                        Extension = reader.DecodeEncryptableString(3, null),
                        ContentType = reader.DecodeEncryptableString(4, null),
                        SizeInBytes = reader.GetInt64(5),
                        CreatedAt = reader.GetDateTimeOffsetOrNull(6)?.UtcDateTime
                    }),
                name: "agent.workspace_content.files")
            .WithParameter("$workspaceId", workspace.Id)
            .WithParameter("$folderId", folderId)
            .WithParameter("$afterId", afterId)
            .WithParameter("$take", take)
            .Execute();
    }

    private readonly record struct CurrentFolder(
        int Id,
        string ExternalId,
        string Name,
        string? AncestorFolderIdsJson);

    private static CurrentFolder? GetCurrentFolder(
        SqliteConnection connection,
        WorkspaceContext workspace,
        FolderExtId folderExternalId)
    {
        var result = connection
            .OneRowCmd(
                sql: """
                     SELECT
                         fo_id,
                         fo_external_id,
                         fo_name,
                         fo_ancestor_folder_ids
                     FROM fo_folders
                     WHERE
                         fo_external_id = $folderExternalId
                         AND fo_workspace_id = $workspaceId
                         AND fo_is_being_deleted = FALSE
                     """,
                readRowFunc: reader => new CurrentFolder(
                    Id: reader.GetInt32(0),
                    ExternalId: reader.GetString(1),
                    Name: reader.DecodeEncryptableString(2, null),
                    AncestorFolderIdsJson: reader.GetStringOrNull(3)),
                name: "agent.workspace_content.current_folder")
            .WithParameter("$folderExternalId", folderExternalId.Value)
            .WithParameter("$workspaceId", workspace.Id)
            .Execute();

        return result.IsEmpty ? null : result.Value;
    }

    private static List<WorkspaceContentFolderDto> GetAncestors(
        SqliteConnection connection,
        WorkspaceContext workspace,
        string? ancestorFolderIdsJson)
    {
        if (string.IsNullOrWhiteSpace(ancestorFolderIdsJson)
            || ancestorFolderIdsJson == "[]")
            return [];

        return connection
            .Cmd(
                sql: """
                     SELECT
                         fo_external_id,
                         fo_name
                     FROM fo_folders
                     WHERE
                         fo_id IN (SELECT value FROM json_each($ancestorFolderIdsJson))
                         AND fo_workspace_id = $workspaceId
                         AND fo_is_being_deleted = FALSE
                     ORDER BY json_array_length(fo_ancestor_folder_ids)
                     """,
                readRowFunc: reader => new WorkspaceContentFolderDto
                {
                    ExternalId = reader.GetString(0),
                    Name = reader.DecodeEncryptableString(1, null)
                },
                name: "agent.workspace_content.ancestors")
            .WithParameter("$ancestorFolderIdsJson", ancestorFolderIdsJson)
            .WithParameter("$workspaceId", workspace.Id)
            .Execute();
    }
}
