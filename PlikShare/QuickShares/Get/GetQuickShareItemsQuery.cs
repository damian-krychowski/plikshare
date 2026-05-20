using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Files.Id;
using PlikShare.Folders;
using PlikShare.Folders.Id;
using PlikShare.QuickShares.Get.Contracts;

namespace PlikShare.QuickShares.Get;

public class GetQuickShareItemsQuery(PlikShareDb plikShareDb)
{
    public GetQuickShareItemsDto Execute(int quickShareId)
    {
        using var connection = plikShareDb.OpenConnection();

        var fileRows = connection
            .Cmd(
                sql: """
                     SELECT
                         fi_external_id,
                         qshi_is_excluded
                     FROM qshi_quick_share_items
                     INNER JOIN fi_files ON fi_id = qshi_file_id
                     WHERE
                         qshi_quick_share_id = $quickShareId
                         AND qshi_file_id IS NOT NULL
                         AND fi_deleted_at IS NULL
                     """,
                readRowFunc: reader => new ItemRow<FileExtId>(
                    ExternalId: reader.GetExtId<FileExtId>(0),
                    IsExcluded: reader.GetBoolean(1)))
            .WithParameter("$quickShareId", quickShareId)
            .Execute();

        var folderRows = connection
            .Cmd(
                sql: """
                     SELECT
                         fo_external_id,
                         qshi_is_excluded
                     FROM qshi_quick_share_items
                     INNER JOIN fo_folders ON fo_id = qshi_folder_id
                     WHERE
                         qshi_quick_share_id = $quickShareId
                         AND qshi_folder_id IS NOT NULL
                         AND fo_is_being_deleted = FALSE
                     """,
                readRowFunc: reader => new ItemRow<FolderExtId>(
                    ExternalId: reader.GetExtId<FolderExtId>(0),
                    IsExcluded: reader.GetBoolean(1)))
            .WithParameter("$quickShareId", quickShareId)
            .Execute();

        // Per item we return an explicit folder path (root → deepest folder to expand) so the
        // FE keeps the parent→child relationship instead of reconstructing it from a flat list:
        //   - folder item: path = its ancestor chain (parents only — F itself is visible once
        //     its parent is expanded; nested items inside F bring their own path that includes F)
        //   - file item: path = parent's ancestor chain + parent (parent must be expanded for Y)
        // Items at workspace root (no ancestor folders) produce no path. Paths are sorted
        // root→deepest internally; duplicates across items are deduped in C#.
        var rawPaths = connection
            .Cmd(
                sql: """
                     SELECT (
                         SELECT json_group_array(sub.fo_external_id)
                         FROM (
                             SELECT af.fo_external_id
                             FROM fo_folders af
                             WHERE af.fo_id IN (SELECT value FROM json_each(f.fo_ancestor_folder_ids))
                                   AND af.fo_is_being_deleted = FALSE
                             ORDER BY json_array_length(af.fo_ancestor_folder_ids) ASC
                         ) AS sub
                     ) AS path_json
                     FROM qshi_quick_share_items
                     INNER JOIN fo_folders AS f ON f.fo_id = qshi_folder_id
                     WHERE qshi_quick_share_id = $quickShareId
                       AND qshi_folder_id IS NOT NULL
                       AND f.fo_is_being_deleted = FALSE
                       AND json_array_length(f.fo_ancestor_folder_ids) > 0

                     UNION ALL

                     SELECT (
                         SELECT json_group_array(sub.fo_external_id)
                         FROM (
                             SELECT af.fo_external_id
                             FROM fo_folders af
                             WHERE (af.fo_id IN (SELECT value FROM json_each(pf.fo_ancestor_folder_ids))
                                    OR af.fo_id = pf.fo_id)
                                   AND af.fo_is_being_deleted = FALSE
                             ORDER BY json_array_length(af.fo_ancestor_folder_ids) ASC
                         ) AS sub
                     ) AS path_json
                     FROM qshi_quick_share_items
                     INNER JOIN fi_files ON fi_id = qshi_file_id
                     INNER JOIN fo_folders AS pf ON pf.fo_id = fi_folder_id
                     WHERE qshi_quick_share_id = $quickShareId
                       AND qshi_file_id IS NOT NULL
                       AND fi_deleted_at IS NULL
                       AND pf.fo_is_being_deleted = FALSE
                     """,
                readRowFunc: reader => reader.GetFromJson<List<FolderExtId>>(0))
            .WithParameter("$quickShareId", quickShareId)
            .Execute();

        var seenPathHashes = new HashSet<int>();
        var foldersToExpand = new List<FolderPath>();

        foreach (var path in rawPaths)
        {
            if (path.Count == 0)
                continue;

            if (seenPathHashes.Add(path.ComputeSequenceHash()))
                foldersToExpand.Add(new FolderPath(path));
        }

        return new GetQuickShareItemsDto(
            SelectedFiles: fileRows.Where(r => !r.IsExcluded).Select(r => r.ExternalId).ToList(),
            ExcludedFiles: fileRows.Where(r => r.IsExcluded).Select(r => r.ExternalId).ToList(),
            SelectedFolders: folderRows.Where(r => !r.IsExcluded).Select(r => r.ExternalId).ToList(),
            ExcludedFolders: folderRows.Where(r => r.IsExcluded).Select(r => r.ExternalId).ToList(),
            FoldersToExpand: foldersToExpand);
    }

    private readonly record struct ItemRow<T>(
        T ExternalId,
        bool IsExcluded);
}
