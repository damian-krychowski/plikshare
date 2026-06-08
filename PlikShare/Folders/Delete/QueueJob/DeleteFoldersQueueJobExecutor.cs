using PlikShare.Boxes.Cache;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Queue;
using PlikShare.Core.Utils;
using Serilog;

namespace PlikShare.Folders.Delete.QueueJob;

public class DeleteFoldersQueueJobExecutor(
    DbWriteQueue dbWriteQueue,
    BulkDeleteFoldersWithDependenciesQuery bulkDeleteFoldersWithDependenciesQuery,
    BoxCache boxCache) : IQueueNormalJobExecutor
{
    public static string StaticJobType => DeleteFoldersQueueJobType.Value;
    public static int StaticPriority => QueueJobPriority.Normal;

    public string JobType => StaticJobType;
    public int Priority => StaticPriority;

    public async Task<QueueJobResult> Execute(
        string definitionJson,
        Guid correlationId,
        CancellationToken cancellationToken)
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

        var result = await dbWriteQueue.Execute(
            operationToEnqueue: context =>
            {
                using var transaction = context.Connection.BeginTransaction();

                try
                {
                    var deleteResult = bulkDeleteFoldersWithDependenciesQuery.Execute(
                        workspaceId: definition.WorkspaceId,
                        folderIds: definition.FolderIds,
                        deletedAt: definition.DeletedAt,
                        correlationId: correlationId,
                        dbWriteContext: context,
                        transaction: transaction);

                    transaction.Commit();

                    return deleteResult;
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            },
            cancellationToken: cancellationToken);

        await ClearCache(result, cancellationToken);

        return QueueJobResult.Success;
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
