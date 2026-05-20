using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.GetSize;
using Serilog;

namespace PlikShare.Trash.Empty;

/// <summary>
/// Permanently deletes every trashed file in the workspace at once. Owner/admin-only at the
/// endpoint level. Implementation defers to <see cref="PurgeFilesSubQuery"/> for the actual
/// row removal + storage purge job emission.
/// </summary>
public class EmptyTrashQuery(
    DbWriteQueue dbWriteQueue,
    PurgeFilesSubQuery purgeFilesSubQuery,
    GetWorkspaceSizeQuery getWorkspaceSizeQuery)
{
    public async Task<Result> Execute(
        WorkspaceContext workspace,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        var deletedCount = await dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                workspace: workspace,
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
        Guid correlationId)
    {
        using var transaction = dbWriteContext.Connection.BeginTransaction();

        try
        {
            dbWriteContext.DeferForeignKeys(transaction);

            var fileIds = dbWriteContext
                .Cmd(
                    sql: """
                         SELECT fi_id
                         FROM fi_files
                         WHERE fi_workspace_id = $workspaceId
                           AND fi_deleted_at IS NOT NULL
                           AND fi_parent_file_id IS NULL
                         """,
                    readRowFunc: reader => reader.GetInt32(0),
                    transaction: transaction)
                .WithParameter("$workspaceId", workspace.Id)
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
                "Emptied trash in Workspace#{WorkspaceId}: {Count} files purged",
                workspace.Id,
                deletedCount);

            return deletedCount;
        }
        catch (Exception e)
        {
            transaction.Rollback();
            Log.Error(e, "EmptyTrash failed for Workspace#{WorkspaceId}", workspace.Id);
            throw;
        }
    }

    public readonly record struct Result(int DeletedCount, long NewWorkspaceSizeInBytes);
}
