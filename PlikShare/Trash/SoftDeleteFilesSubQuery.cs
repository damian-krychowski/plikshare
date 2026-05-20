using Microsoft.Data.Sqlite;
using PlikShare.Core.SQLite;
using PlikShare.Files.Id;

namespace PlikShare.Trash;

/// <summary>
/// Soft-deletes files: stamps <c>fi_deleted_at</c> + <c>fi_original_folder_path</c> in place,
/// detaches them from listing surfaces (via the <c>fi_deleted_at IS NULL</c> filter added to
/// every listing query), and emits NO storage-purge jobs. Mirror of
/// <see cref="PlikShare.Files.Delete.DeleteFilesSubQuery"/> but transactional state, not
/// physical removal.
///
/// Child files (those with <c>fi_parent_file_id</c> pointing at a soft-deleted main file —
/// thumbnails, OCR artifacts, etc.) are soft-deleted with NO path snapshot. They're never
/// shown in trash UI; restore of the parent cascades to them automatically.
/// </summary>
public class SoftDeleteFilesSubQuery(PathSnapshotBuilder pathSnapshotBuilder)
{
    public Result Execute(
        int workspaceId,
        List<int> fileIds,
        DateTimeOffset deletedAt,
        SqliteWriteContext dbWriteContext,
        SqliteTransaction transaction)
    {
        if (fileIds.Count == 0)
            return new Result(SoftDeletedFiles: []);

        // Step 1: snapshot what each main file's parent folder is. Skip files that are
        // already in trash (idempotency) and skip children — they don't get their own snapshots.
        var mainFiles = dbWriteContext
            .Cmd(
                sql: @"
                    SELECT fi_id, fi_folder_id
                    FROM fi_files
                    WHERE fi_workspace_id = $workspaceId
                      AND fi_id IN (SELECT value FROM json_each($fileIds))
                      AND fi_deleted_at IS NULL
                      AND fi_parent_file_id IS NULL
                ",
                readRowFunc: reader => new MainFileRow(
                    FileId: reader.GetInt32(0),
                    FolderId: reader.GetInt32OrNull(1)),
                transaction: transaction)
            .WithParameter("$workspaceId", workspaceId)
            .WithJsonParameter("$fileIds", fileIds)
            .Execute();

        if (mainFiles.Count == 0)
            return new Result(SoftDeletedFiles: []);

        // Step 2: build snapshots for every distinct folder these files live in.
        var folderIds = mainFiles
            .Where(f => f.FolderId.HasValue)
            .Select(f => f.FolderId!.Value)
            .Distinct()
            .ToArray();

        var snapshotsByFolder = pathSnapshotBuilder.BuildPaths(
            workspaceId: workspaceId,
            folderIds: folderIds,
            dbWriteContext: dbWriteContext,
            transaction: transaction);

        // Step 3: UPDATE files grouped by their folder so each group gets one path snapshot.
        // Files in the workspace root (fi_folder_id IS NULL) get a NULL snapshot — restore
        // puts them back at the root, which is what the snapshot would describe anyway.
        var softDeletedFiles = new List<SoftDeletedFile>(
            mainFiles.Count);

        foreach (var group in mainFiles.GroupBy(f => f.FolderId))
        {
            var ids = group
                .Select(f => f.FileId)
                .ToArray();

            var path = group.Key.HasValue
                && snapshotsByFolder.TryGetValue(group.Key.Value, out var segments)
                    ? segments
                    : null;

            // Detach from the folder: this lets folder-level hard-deletes (the existing
            // BulkDeleteFoldersWithDependenciesQuery, which selects "WHERE fi_folder_id IN
            // (deletedFolderIds)") skip trashed files. Without this nullification, soft-deleted
            // files would be physically wiped the moment their parent folder is removed —
            // silently losing trash contents.
            var updated = dbWriteContext
                .Cmd(
                    sql: @"
                        UPDATE fi_files
                        SET fi_deleted_at = $deletedAt,
                            fi_original_folder_path = $pathJson,
                            fi_folder_id = NULL
                        WHERE fi_workspace_id = $workspaceId
                          AND fi_id IN (SELECT value FROM json_each($fileIds))
                          AND fi_deleted_at IS NULL
                        RETURNING fi_id, fi_external_id
                    ",
                    readRowFunc: reader => new SoftDeletedFile(
                        Id: reader.GetInt32(0),
                        ExternalId: reader.GetExtId<FileExtId>(1)),
                    transaction: transaction)
                .WithParameter("$workspaceId", workspaceId)
                .WithParameter("$deletedAt", deletedAt)
                .WithJsonParameter("$pathJson", path)
                .WithJsonParameter("$fileIds", ids)
                .Execute();

            softDeletedFiles.AddRange(updated);
        }

        // Step 4: cascade to dependent files (artifacts: thumbnails, OCR output, etc.).
        // No snapshot — they're not user-visible in trash UI; restoring the parent file
        // un-trashes them in one shot via the same fi_parent_file_id link.
        var mainIds = softDeletedFiles
            .Select(f => f.Id)
            .ToArray();

        if (mainIds.Length > 0)
        {
            var children = dbWriteContext
                .Cmd(
                    sql: @"
                        UPDATE fi_files
                        SET fi_deleted_at = $deletedAt,
                            fi_original_folder_path = NULL,
                            fi_folder_id = NULL
                        WHERE fi_workspace_id = $workspaceId
                          AND fi_parent_file_id IN (SELECT value FROM json_each($mainIds))
                          AND fi_deleted_at IS NULL
                        RETURNING fi_id, fi_external_id
                    ",
                    readRowFunc: reader => new SoftDeletedFile(
                        Id: reader.GetInt32(0),
                        ExternalId: reader.GetExtId<FileExtId>(1)),
                    transaction: transaction)
                .WithParameter("$workspaceId", workspaceId)
                .WithParameter("$deletedAt", deletedAt)
                .WithJsonParameter("$mainIds", mainIds)
                .Execute();

            softDeletedFiles.AddRange(children);
        }

        return new Result(SoftDeletedFiles: softDeletedFiles);
    }

    private readonly record struct MainFileRow(int FileId, int? FolderId);

    public readonly record struct SoftDeletedFile(int Id, FileExtId ExternalId);

    public readonly record struct Result(List<SoftDeletedFile> SoftDeletedFiles);
}
