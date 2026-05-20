using Microsoft.Data.Sqlite;
using PlikShare.Core.SQLite;

namespace PlikShare.Trash;

// Builds the root → leaf folder-ancestry chains for a batch of folder IDs in one query. Used by
// soft-delete to snapshot each trashed file's fi_original_folder_path. Folders not found (already
// deleted, wrong workspace) are simply absent from the result.
public class PathSnapshotBuilder
{
    public Dictionary<int, List<OriginalFolderPathSegment>> BuildPaths(
        int workspaceId,
        int[] folderIds,
        SqliteWriteContext dbWriteContext,
        SqliteTransaction transaction)
    {
        if (folderIds.Length == 0)
            return new Dictionary<int, List<OriginalFolderPathSegment>>();

        // One query gives us, for each requested leaf folder, the (leaf + every ancestor)
        // rows tagged with depth so the DB sorts them root → leaf. Self-row uses the leaf's
        // own ancestor count as its depth; ancestor rows use their own. Both interleave into
        // the right order via ORDER BY (leaf_id, depth) — the fold below just appends in
        // arrival order, so each leaf's segment list comes out root → leaf.
        return dbWriteContext.AggregateRows(
            sql: @"
                WITH leaves AS (
                    SELECT fo_id, fo_name, fo_ancestor_folder_ids
                    FROM fo_folders
                    WHERE fo_workspace_id = $workspaceId
                      AND fo_id IN (SELECT value FROM json_each($folderIds))
                )
                SELECT
                    l.fo_id AS leaf_id,
                    a.fo_id AS folder_id,
                    a.fo_name AS folder_name,
                    json_array_length(a.fo_ancestor_folder_ids) AS depth
                FROM leaves l
                JOIN fo_folders a
                  ON a.fo_workspace_id = $workspaceId
                 AND a.fo_id IN (SELECT value FROM json_each(l.fo_ancestor_folder_ids))

                UNION ALL

                SELECT
                    l.fo_id AS leaf_id,
                    l.fo_id AS folder_id,
                    l.fo_name AS folder_name,
                    json_array_length(l.fo_ancestor_folder_ids) AS depth
                FROM leaves l

                ORDER BY leaf_id, depth
            ",
            seed: new Dictionary<int, List<OriginalFolderPathSegment>>(folderIds.Length),
            aggregateRowFunc: (acc, reader) =>
            {
                var leafId = reader.GetInt32(0);

                if (!acc.TryGetValue(leafId, out var segments))
                {
                    segments = [];
                    acc[leafId] = segments;
                }

                segments.Add(new OriginalFolderPathSegment(
                    FolderId: reader.GetInt32(1),
                    Name: reader.GetString(2)));

                return acc;
            },
            transaction: transaction)
        .WithParameter("$workspaceId", workspaceId)
        .WithJsonParameter("$folderIds", folderIds)
        .Execute();
    }
}
