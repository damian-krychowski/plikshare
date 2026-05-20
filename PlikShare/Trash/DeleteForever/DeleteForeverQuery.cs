using Microsoft.Data.Sqlite;
using PlikShare.Core.Database.MainDatabase;
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
        var deletedCount = await dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                workspace: workspace,
                fileExternalIds: fileExternalIds,
                correlationId: correlationId),
            cancellationToken: cancellationToken);

        var newSize = getWorkspaceSizeQuery.Execute(workspace);

        return new Result(
            DeletedCount: deletedCount,
            NewWorkspaceSizeInBytes: newSize);
    }

    private int ExecuteOperation(
        SqliteWriteContext dbWriteContext,
        WorkspaceContext workspace,
        List<FileExtId> fileExternalIds,
        Guid correlationId)
    {
        if (fileExternalIds.Count == 0)
            return 0;

        using var transaction = dbWriteContext.Connection.BeginTransaction();

        try
        {
            dbWriteContext.DeferForeignKeys(transaction);

            // Only trashed files of this workspace are eligible. A request that smuggles in a
            // live file's external id silently drops it here — preserving the invariant that
            // delete-forever is a trash-only operation.
            var fileIds = dbWriteContext
                .Cmd(
                    sql: """
                         SELECT fi_id
                         FROM fi_files
                         WHERE fi_workspace_id = $workspaceId
                           AND fi_deleted_at IS NOT NULL
                           AND fi_parent_file_id IS NULL
                           AND fi_external_id IN (SELECT value FROM json_each($externalIds))
                         """,
                    readRowFunc: reader => reader.GetInt32(0),
                    transaction: transaction)
                .WithParameter("$workspaceId", workspace.Id)
                .WithJsonParameter("$externalIds", fileExternalIds)
                .Execute();

            if (fileIds.Count == 0)
            {
                transaction.Commit();
                return 0;
            }

            var deletedCount = purgeFilesSubQuery.Execute(
                workspaceId: workspace.Id,
                fileIds: fileIds,
                correlationId: correlationId,
                dbWriteContext: dbWriteContext,
                transaction: transaction);

            transaction.Commit();

            Log.Information(
                "Permanently deleted {Count} trashed files in Workspace#{WorkspaceId}",
                deletedCount,
                workspace.Id);

            return deletedCount;
        }
        catch (Exception e)
        {
            transaction.Rollback();
            Log.Error(e, "DeleteForever failed for Workspace#{WorkspaceId}", workspace.Id);
            throw;
        }
    }

    public readonly record struct Result(int DeletedCount, long NewWorkspaceSizeInBytes);
}
