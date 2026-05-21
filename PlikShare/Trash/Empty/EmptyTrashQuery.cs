using PlikShare.AuditLog.Details;
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
        var purge = await dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                workspace: workspace,
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
        Guid correlationId)
    {
        using var transaction = dbWriteContext.Connection.BeginTransaction();

        try
        {
            dbWriteContext.DeferForeignKeys(transaction);

            // File details are captured now, before the purge wipes the rows, so the audit
            // log can record exactly what was emptied.
            var rows = dbWriteContext
                .Cmd(
                    sql: """
                         SELECT fi_id, fi_external_id, fi_name, fi_extension, fi_size_in_bytes,
                                fi_original_folder_path
                         FROM fi_files
                         WHERE fi_workspace_id = $workspaceId
                           AND fi_deleted_at IS NOT NULL
                           AND fi_parent_file_id IS NULL
                         """,
                    readRowFunc: TrashedFileRow.Read,
                    transaction: transaction)
                .WithParameter("$workspaceId", workspace.Id)
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
                "Emptied trash in Workspace#{WorkspaceId}: {Count} files purged",
                workspace.Id,
                deletedCount);

            return new PurgeResult(
                DeletedCount: deletedCount,
                Files: rows.Select(r => r.FileRef).ToList());
        }
        catch (Exception e)
        {
            transaction.Rollback();
            Log.Error(e, "EmptyTrash failed for Workspace#{WorkspaceId}", workspace.Id);
            throw;
        }
    }

    private readonly record struct PurgeResult(int DeletedCount, List<Audit.FileRef> Files);

    public readonly record struct Result(
        int DeletedCount,
        long NewWorkspaceSizeInBytes,
        List<Audit.FileRef> Files);
}
