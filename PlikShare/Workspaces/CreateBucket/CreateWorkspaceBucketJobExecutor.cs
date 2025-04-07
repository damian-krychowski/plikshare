using PlikShare.Core.Queue;
using PlikShare.Core.Utils;
using PlikShare.Storages;
using PlikShare.Workspaces.Cache;
using Serilog;

namespace PlikShare.Workspaces.CreateBucket;

public class CreateWorkspaceBucketJobExecutor(
    StorageClientStore storageClientStore,
    UpdateWorkspaceIsBucketCreatedQuery updateWorkspaceIsBucketCreatedQuery,
    WorkspaceCache workspaceCache) : IQueueNormalJobExecutor
{
    public string JobType => CreateWorkspaceBucketQueueJobType.Value;
    public int Priority => QueueJobPriority.ExtremelyHigh;

    public async Task<QueueJobResult> Execute(
        string definitionJson, 
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        var definition = Json.Deserialize<CreateWorkspaceBucketQueueJobDefinition>(
            definitionJson);

        if (definition is null)
        {
            throw new ArgumentException(
                $"Job '{definitionJson}' cannot be parsed to correct '{nameof(CreateWorkspaceBucketQueueJobDefinition)}'");
        }

        if (!storageClientStore.TryGetClient(definition.StorageId, out var storage))
        {
            Log.Warning("Could not create workspace bucket for Workspace#{WorkspaceId} because Storage#{StorageId} was not found. Marking the queue job as completed.",
                definition.WorkspaceId,
                definition.StorageId);

            return QueueJobResult.Success;
        }

        await storage.CreateBucketIfDoesntExist(
            bucketName: definition.BucketName,
            cancellationToken: cancellationToken);
        
        Log.Information("Bucket '{BucketName}' for Workspace#{WorkspaceId} in Storage#{StorageId} was created.",
            definition.BucketName,
            definition.WorkspaceId,
            definition.StorageId);

        var result = await updateWorkspaceIsBucketCreatedQuery.Execute(
            workspaceId: definition.WorkspaceId,
            cancellationToken: cancellationToken);

        if (result == UpdateWorkspaceIsBucketCreatedQuery.ResultCode.Ok)
        {
            await workspaceCache.InvalidateEntry(
                workspaceId: definition.WorkspaceId,
                cancellationToken: cancellationToken);
        }
        else if (result == UpdateWorkspaceIsBucketCreatedQuery.ResultCode.NotFound)
        {
            await storage.DeleteBucket(
                bucketName: definition.BucketName,
                cancellationToken: cancellationToken);

            Log.Warning("Bucket '{BucketName}' for Workspace#{WorkspaceId} in Storage#{StorageId} was deleted because workspace had been deleted in the meantime.",
                definition.BucketName,
                definition.WorkspaceId,
                definition.StorageId);
        }
        else
        {
            throw new ArgumentOutOfRangeException(
                paramName: nameof(UpdateWorkspaceIsBucketCreatedQuery.ResultCode),
                message: $"Unknown value of result code: {result}");
        }
        
        return QueueJobResult.Success;
    }
}