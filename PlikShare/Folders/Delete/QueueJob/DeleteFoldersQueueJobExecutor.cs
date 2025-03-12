using Microsoft.Data.Sqlite;
using PlikShare.Boxes.Cache;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Queue;
using PlikShare.Core.Utils;
using Serilog;

namespace PlikShare.Folders.Delete.QueueJob;

public class DeleteFoldersQueueJobExecutor(
    BulkDeleteFoldersWithDependenciesQuery bulkDeleteFoldersWithDependenciesQuery,
    BoxCache boxCache) : IQueueDbOnlyJobExecutor
{
    public string JobType => DeleteFoldersQueueJobType.Value;
    public int Priority => QueueJobPriority.Normal;

    public (QueueJobResult Result, Func<CancellationToken, ValueTask> SideEffectsToRun) Execute(
        string definitionJson, 
        Guid correlationId, 
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction)
    {
        var definition = Json.Deserialize<DeleteFoldersQueueJobDefinition>(
            definitionJson);

        if (definition is null)
        {
            throw new ArgumentException(
                $"Job '{definitionJson}' cannot be parsed to correct '{nameof(DeleteFoldersQueueJobDefinition)}'");
        }
        
        Log.Information("Delete Folders (count: {FoldersCount}) in Workspace#{WorkspaceId} operation started",
            definition.FolderIds.Length,
            definition.WorkspaceId);
        
        var result = bulkDeleteFoldersWithDependenciesQuery.Execute(
            workspaceId: definition.WorkspaceId,
            folderIds: definition.FolderIds,
            deletedAt: definition.DeletedAt,
            correlationId: correlationId,
            dbWriteContext: dbWriteContext,
            transaction: transaction);

        return (
            Result: QueueJobResult.Success, 
            SideEffectsToRun: cancellationToken => ClearCache(result, cancellationToken)
        );
    }

    private async ValueTask ClearCache(
        BulkDeleteFoldersWithDependenciesQuery.Result result,
        CancellationToken cancellationToken)
    {
        foreach (var boxExternalId in result.DetachedBoxes)
        {
            await boxCache.InvalidateEntry(
                boxExternalId: boxExternalId,
                cancellationToken: cancellationToken);
        }
    }
}