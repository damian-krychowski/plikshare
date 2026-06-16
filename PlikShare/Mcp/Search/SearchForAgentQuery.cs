using Microsoft.Data.Sqlite;
using PlikShare.Agents.Cache;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Mcp.Search.Contracts;

namespace PlikShare.Mcp.Search;

public class SearchForAgentQuery(PlikShareDb plikShareDb)
{
    public readonly record struct Result(
        List<SearchEntryDto> Entries,
        SearchCursor? NextCursor,
        bool HasMore);

    public sealed record Filters(
        List<string> WorkspaceExternalIds,
        List<string> FolderExternalIds,
        List<string> ExcludeWorkspaceExternalIds,
        List<string> ExcludeFolderExternalIds,
        bool IncludeFolders,
        bool IncludeFiles,
        List<string> NameContains,
        List<string> Extensions,
        List<string> ContentTypesExact,
        List<string> ContentTypesPrefix,
        DateTimeOffset? CreatedAfter,
        DateTimeOffset? CreatedBefore,
        long? SizeMin,
        long? SizeMax);

    public Result Execute(
        AgentContext agent,
        Filters filters,
        SearchCursor? cursor,
        int limit)
    {
        using var connection = plikShareDb.OpenConnection();

        var entries = new List<SearchEntryDto>();

        if (filters.IncludeFolders && (cursor is null || cursor.Phase == SearchPhase.Folder))
        {
            var folderBeforeId = cursor?.Phase == SearchPhase.Folder ? cursor.LastId : (int?)null;

            var folders = QueryFolders(connection, agent, filters, folderBeforeId, limit + 1);

            if (folders.Count > limit)
            {
                var page = folders.Take(limit).ToList();
                entries.AddRange(page.Select(x => x.Entry));

                return new Result(entries, new SearchCursor(SearchPhase.Folder, page[^1].Id), true);
            }

            entries.AddRange(folders.Select(x => x.Entry));

            if (!filters.IncludeFiles)
                return new Result(entries, null, false);

            return FillFiles(connection, agent, filters, entries, fileBeforeId: null, remaining: limit - folders.Count);
        }

        if (filters.IncludeFiles)
        {
            var fileBeforeId = cursor?.Phase == SearchPhase.File ? cursor.LastId : (int?)null;

            return FillFiles(connection, agent, filters, entries, fileBeforeId, remaining: limit);
        }

        return new Result(entries, null, false);
    }

    private Result FillFiles(
        SqliteConnection connection,
        AgentContext agent,
        Filters filters,
        List<SearchEntryDto> entries,
        int? fileBeforeId,
        int remaining)
    {
        if (remaining <= 0)
        {
            // The folder page exactly filled the limit — peek whether any file would follow, and if so
            // hand back a cursor that resumes the file phase from the top on the next call.
            var peek = QueryFiles(connection, agent, filters, fileBeforeId, 1);

            return peek.Count > 0
                ? new Result(entries, new SearchCursor(SearchPhase.File, int.MaxValue), true)
                : new Result(entries, null, false);
        }

        var files = QueryFiles(connection, agent, filters, fileBeforeId, remaining + 1);

        if (files.Count > remaining)
        {
            var page = files.Take(remaining).ToList();
            entries.AddRange(page.Select(x => x.Entry));

            return new Result(entries, new SearchCursor(SearchPhase.File, page[^1].Id), true);
        }

        entries.AddRange(files.Select(x => x.Entry));

        return new Result(entries, null, false);
    }

    private const string AccessibleAndTargetFoldersCte =
        """
        WITH accessible AS (
            SELECT w_id, w_external_id
            FROM w_workspaces
            WHERE w_is_being_deleted = FALSE
                AND w_encryption_salt IS NULL
                AND (
                    $isAdmin
                    OR w_owner_agent_id = $agentId
                    OR EXISTS (
                        SELECT 1 FROM wa_workspace_agents
                        WHERE wa_workspace_id = w_id AND wa_agent_id = $agentId
                    )
                )
                AND (
                    json_array_length($workspaceExternalIds) = 0
                    OR w_external_id IN (SELECT value FROM json_each($workspaceExternalIds))
                )
                AND (
                    json_array_length($excludeWorkspaceExternalIds) = 0
                    OR w_external_id NOT IN (SELECT value FROM json_each($excludeWorkspaceExternalIds))
                )
        ),
        target_folders AS (
            SELECT fo_id
            FROM fo_folders
            WHERE fo_is_being_deleted = FALSE
                AND fo_external_id IN (SELECT value FROM json_each($folderExternalIds))
                AND fo_workspace_id IN (SELECT w_id FROM accessible)
        ),
        excluded_folders AS (
            SELECT fo_id
            FROM fo_folders
            WHERE fo_external_id IN (SELECT value FROM json_each($excludeFolderExternalIds))
                AND fo_workspace_id IN (SELECT w_id FROM accessible)
        )
        """;

    private static List<(int Id, SearchEntryDto Entry)> QueryFiles(
        SqliteConnection connection,
        AgentContext agent,
        Filters f,
        int? beforeId,
        int take)
    {
        return connection
            .Cmd(
                sql: $"""
                     {AccessibleAndTargetFoldersCte}
                     SELECT
                         fi.fi_id,
                         fi.fi_external_id,
                         fi.fi_name,
                         fi.fi_extension,
                         fi.fi_content_type,
                         fi.fi_size_in_bytes,
                         fi.fi_created_at,
                         parent.fo_external_id,
                         acc.w_external_id
                     FROM fi_files AS fi
                     JOIN accessible AS acc ON acc.w_id = fi.fi_workspace_id
                     LEFT JOIN fo_folders AS parent ON parent.fo_id = fi.fi_folder_id
                     WHERE
                         fi.fi_parent_file_id IS NULL
                         AND fi.fi_deleted_at IS NULL
                         AND fi.fi_is_upload_completed = TRUE
                         AND (parent.fo_id IS NULL OR parent.fo_is_being_deleted = FALSE)
                         AND ($beforeId IS NULL OR fi.fi_id < $beforeId)
                         AND (
                             json_array_length($folderExternalIds) = 0
                             OR fi.fi_folder_id IN (SELECT fo_id FROM target_folders)
                             OR (
                                 parent.fo_ancestor_folder_ids IS NOT NULL
                                 AND EXISTS (
                                     SELECT 1 FROM json_each(parent.fo_ancestor_folder_ids) AS anc
                                     WHERE anc.value IN (SELECT fo_id FROM target_folders)
                                 )
                             )
                         )
                         AND (
                             json_array_length($excludeFolderExternalIds) = 0
                             OR NOT (
                                 fi.fi_folder_id IN (SELECT fo_id FROM excluded_folders)
                                 OR (
                                     parent.fo_ancestor_folder_ids IS NOT NULL
                                     AND EXISTS (
                                         SELECT 1 FROM json_each(parent.fo_ancestor_folder_ids) AS anc
                                         WHERE anc.value IN (SELECT fo_id FROM excluded_folders)
                                     )
                                 )
                             )
                         )
                         AND (
                             json_array_length($nameContains) = 0
                             OR EXISTS (
                                 SELECT 1 FROM json_each($nameContains) AS n
                                 WHERE instr(LOWER(fi.fi_name), LOWER(n.value)) > 0
                             )
                         )
                         AND (
                             json_array_length($extensions) = 0
                             OR LOWER(LTRIM(fi.fi_extension, '.')) IN (SELECT value FROM json_each($extensions))
                         )
                         AND (
                             (json_array_length($contentTypesExact) = 0 AND json_array_length($contentTypesPrefix) = 0)
                             OR LOWER(fi.fi_content_type) IN (SELECT value FROM json_each($contentTypesExact))
                             OR EXISTS (
                                 SELECT 1 FROM json_each($contentTypesPrefix) AS p
                                 WHERE LOWER(fi.fi_content_type) LIKE p.value || '%'
                             )
                         )
                         AND ($createdAfter IS NULL OR fi.fi_created_at >= $createdAfter)
                         AND ($createdBefore IS NULL OR fi.fi_created_at <= $createdBefore)
                         AND ($sizeMin IS NULL OR fi.fi_size_in_bytes >= $sizeMin)
                         AND ($sizeMax IS NULL OR fi.fi_size_in_bytes <= $sizeMax)
                     ORDER BY fi.fi_id DESC
                     LIMIT $take
                     """,
                readRowFunc: reader => (
                    Id: reader.GetInt32(0),
                    Entry: new SearchEntryDto
                    {
                        Type = "file",
                        ExternalId = reader.GetString(1),
                        Name = reader.DecodeEncryptableString(2, null),
                        Extension = reader.DecodeEncryptableString(3, null),
                        ContentType = reader.DecodeEncryptableString(4, null),
                        SizeInBytes = reader.GetInt64(5),
                        CreatedAt = reader.GetDateTimeOffsetOrNull(6)?.UtcDateTime,
                        FolderExternalId = reader.GetStringOrNull(7),
                        WorkspaceExternalId = reader.GetString(8)
                    }),
                name: "agent.search.files")
            .WithParameter("$isAdmin", agent.HasAdminRole)
            .WithParameter("$agentId", agent.Id)
            .WithJsonParameter("$workspaceExternalIds", f.WorkspaceExternalIds)
            .WithJsonParameter("$folderExternalIds", f.FolderExternalIds)
            .WithJsonParameter("$excludeWorkspaceExternalIds", f.ExcludeWorkspaceExternalIds)
            .WithJsonParameter("$excludeFolderExternalIds", f.ExcludeFolderExternalIds)
            .WithParameter("$beforeId", beforeId)
            .WithJsonParameter("$nameContains", f.NameContains)
            .WithJsonParameter("$extensions", f.Extensions)
            .WithJsonParameter("$contentTypesExact", f.ContentTypesExact)
            .WithJsonParameter("$contentTypesPrefix", f.ContentTypesPrefix)
            .WithParameter("$createdAfter", f.CreatedAfter)
            .WithParameter("$createdBefore", f.CreatedBefore)
            .WithParameter("$sizeMin", f.SizeMin)
            .WithParameter("$sizeMax", f.SizeMax)
            .WithParameter("$take", take)
            .Execute();
    }

    private static List<(int Id, SearchEntryDto Entry)> QueryFolders(
        SqliteConnection connection,
        AgentContext agent,
        Filters f,
        int? beforeId,
        int take)
    {
        return connection
            .Cmd(
                sql: $"""
                     {AccessibleAndTargetFoldersCte}
                     SELECT
                         fo.fo_id,
                         fo.fo_external_id,
                         fo.fo_name,
                         fo.fo_created_at,
                         parent.fo_external_id,
                         acc.w_external_id
                     FROM fo_folders AS fo
                     JOIN accessible AS acc ON acc.w_id = fo.fo_workspace_id
                     LEFT JOIN fo_folders AS parent ON parent.fo_id = fo.fo_parent_folder_id
                     WHERE
                         fo.fo_is_being_deleted = FALSE
                         AND ($beforeId IS NULL OR fo.fo_id < $beforeId)
                         AND (
                             json_array_length($folderExternalIds) = 0
                             OR (
                                 fo.fo_ancestor_folder_ids IS NOT NULL
                                 AND EXISTS (
                                     SELECT 1 FROM json_each(fo.fo_ancestor_folder_ids) AS anc
                                     WHERE anc.value IN (SELECT fo_id FROM target_folders)
                                 )
                             )
                         )
                         AND (
                             json_array_length($excludeFolderExternalIds) = 0
                             OR NOT (
                                 fo.fo_id IN (SELECT fo_id FROM excluded_folders)
                                 OR (
                                     fo.fo_ancestor_folder_ids IS NOT NULL
                                     AND EXISTS (
                                         SELECT 1 FROM json_each(fo.fo_ancestor_folder_ids) AS anc
                                         WHERE anc.value IN (SELECT fo_id FROM excluded_folders)
                                     )
                                 )
                             )
                         )
                         AND (
                             json_array_length($nameContains) = 0
                             OR EXISTS (
                                 SELECT 1 FROM json_each($nameContains) AS n
                                 WHERE instr(LOWER(fo.fo_name), LOWER(n.value)) > 0
                             )
                         )
                         AND ($createdAfter IS NULL OR fo.fo_created_at >= $createdAfter)
                         AND ($createdBefore IS NULL OR fo.fo_created_at <= $createdBefore)
                     ORDER BY fo.fo_id DESC
                     LIMIT $take
                     """,
                readRowFunc: reader => (
                    Id: reader.GetInt32(0),
                    Entry: new SearchEntryDto
                    {
                        Type = "folder",
                        ExternalId = reader.GetString(1),
                        Name = reader.DecodeEncryptableString(2, null),
                        Extension = null,
                        ContentType = null,
                        SizeInBytes = null,
                        CreatedAt = reader.GetDateTimeOffsetOrNull(3)?.UtcDateTime,
                        FolderExternalId = reader.GetStringOrNull(4),
                        WorkspaceExternalId = reader.GetString(5)
                    }),
                name: "agent.search.folders")
            .WithParameter("$isAdmin", agent.HasAdminRole)
            .WithParameter("$agentId", agent.Id)
            .WithJsonParameter("$workspaceExternalIds", f.WorkspaceExternalIds)
            .WithJsonParameter("$folderExternalIds", f.FolderExternalIds)
            .WithJsonParameter("$excludeWorkspaceExternalIds", f.ExcludeWorkspaceExternalIds)
            .WithJsonParameter("$excludeFolderExternalIds", f.ExcludeFolderExternalIds)
            .WithParameter("$beforeId", beforeId)
            .WithJsonParameter("$nameContains", f.NameContains)
            .WithParameter("$createdAfter", f.CreatedAfter)
            .WithParameter("$createdBefore", f.CreatedBefore)
            .WithParameter("$take", take)
            .Execute();
    }
}
