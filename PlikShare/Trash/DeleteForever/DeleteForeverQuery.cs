using Microsoft.Data.Sqlite;
using PlikShare.AuditLog.Details;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using PlikShare.Files.Id;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.GetSize;
using Serilog;

namespace PlikShare.Trash.DeleteForever;

/// <summary>
/// Permanently deletes specific trashed files (and their child artifacts). Scopes the candidate
/// set to files that are actually in trash so a malformed request can't accidentally wipe live
/// files, then defers the hard-delete + follow-up job emission to <see cref="PurgeFilesSubQuery"/>.
/// </summary>
public class DeleteForeverQuery(
    DbWriteQueue dbWriteQueue,
    PurgeFilesSubQuery purgeFilesSubQuery,
    GetWorkspaceSizeQuery getWorkspaceSizeQuery)
{
    public async Task<Result> Execute(
        WorkspaceContext workspace,
        List<FileExtId> fileExternalIds,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        var purge = await dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                workspace: workspace,
                fileExternalIds: fileExternalIds,
                correlationId: correlationId),
            cancellationToken: cancellationToken);

        var newSize = getWorkspaceSizeQuery.Execute(workspace);

        return new Result(
            DeletedCount: purge.DeletedCount,
            NewWorkspaceSizeInBytes: newSize,
            Files: purge.Files);
    }

    private PurgeResult ExecuteOperation(
        SqliteWriteContext dbWriteContext,
        WorkspaceContext workspace,
        List<FileExtId> fileExternalIds,
        Guid correlationId)
    {
        if (fileExternalIds.Count == 0)
            return new PurgeResult(DeletedCount: 0, Files: []);

        using var transaction = dbWriteContext.Connection.BeginTransaction();

        try
        {
            dbWriteContext.DeferForeignKeys(transaction);

            // Only trashed files of this workspace are eligible. A request that smuggles in a
            // live file's external id silently drops it here — preserving the invariant that
            // delete-forever is a trash-only operation. File details are captured now, before
            // the purge wipes the rows, so the audit log can record what was deleted.
            var rows = dbWriteContext
                .Cmd(
                    sql: """
                         SELECT fi_id, fi_external_id, fi_name, fi_extension, fi_size_in_bytes,
                                fi_original_folder_path
                         FROM fi_files
                         WHERE fi_workspace_id = $workspaceId
                           AND fi_deleted_at IS NOT NULL
                           AND fi_parent_file_id IS NULL
                           AND fi_external_id IN (SELECT value FROM json_each($externalIds))
                         """,
                    readRowFunc: TrashedFileRow.Read,
                    transaction: transaction)
                .WithParameter("$workspaceId", workspace.Id)
                .WithJsonParameter("$externalIds", fileExternalIds)
                .Execute();

            if (rows.Count == 0)
            {
                transaction.Commit();
                return new PurgeResult(DeletedCount: 0, Files: []);
            }

            var deletedCount = purgeFilesSubQuery.Execute(
                workspaceId: workspace.Id,
                fileIds: rows.Select(r => r.Id).ToList(),
                correlationId: correlationId,
                dbWriteContext: dbWriteContext,
                transaction: transaction);

            transaction.Commit();

            Log.Information(
                "Permanently deleted {Count} trashed files in Workspace#{WorkspaceId}",
                deletedCount,
                workspace.Id);

            return new PurgeResult(
                DeletedCount: deletedCount,
                Files: rows.Select(r => r.FileRef).ToList());
        }
        catch (Exception e)
        {
            transaction.Rollback();
            Log.Error(e, "DeleteForever failed for Workspace#{WorkspaceId}", workspace.Id);
            throw;
        }
    }

    private readonly record struct PurgeResult(int DeletedCount, List<Audit.FileRef> Files);

    public readonly record struct Result(
        int DeletedCount,
        long NewWorkspaceSizeInBytes,
        List<Audit.FileRef> Files);
}
