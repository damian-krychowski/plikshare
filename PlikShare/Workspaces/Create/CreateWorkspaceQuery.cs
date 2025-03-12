using Microsoft.Data.Sqlite;
using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Queue;
using PlikShare.Core.SQLite;
using PlikShare.Storages.Id;
using PlikShare.Workspaces.CreateBucket;
using PlikShare.Workspaces.Id;
using Serilog;

namespace PlikShare.Workspaces.Create;

public class CreateWorkspaceQuery(
    IClock clock,
    IQueue queue,
    DbWriteQueue dbWriteQueue)
{
    public Task<Result> Execute(
        StorageExtId storageExternalId,
        int ownerId,
        string name,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context, 
                storageExternalId: 
                storageExternalId, 
                ownerId: ownerId, 
                name: name, 
                correlationId: correlationId),
            cancellationToken: cancellationToken);
    }

    private Result ExecuteOperation(
        DbWriteQueue.Context dbWriteContext,
        StorageExtId storageExternalId,
        int ownerId,
        string name,
        Guid correlationId)
    {
        using var transaction = dbWriteContext.Connection.BeginTransaction();

        try
        {
            var result = ExecuteTransaction(
                dbWriteContext: dbWriteContext, 
                storageExternalId: storageExternalId, 
                ownerId: ownerId, 
                name: name, 
                correlationId: correlationId, 
                transaction: transaction);
            
            transaction.Commit();
            
            Log.Information(
                "Workspace '{WorkspaceExternalId} ({WorkspaceId})' with bucket name '{BucketName}' was created.",
                result.Workspace.ExternalId,
                result.Workspace.Id,
                result.Workspace.BucketName);

            return result;
        }
        catch (SqliteException e)
        {
            transaction.Rollback();

            if (e.HasForeignKeyFailed())
                return new Result(Code: ResultCode.StorageNotFound);

            if (e.HasNotNullConstraintFailed(tableName: "w_workspaces", columnName: "w_storage_id"))
                return new Result(Code: ResultCode.StorageNotFound);

            Log.Error(e, "Something went wrong while creating Workspace '{WorkspaceName}' for Owner '{OwnerId}'",
                name,
                ownerId);
            
            throw;
        }
        catch (Exception e)
        {
            transaction.Rollback();

            Log.Error(e, "Something went wrong while creating Workspace '{WorkspaceName}' for Owner '{OwnerId}'",
                name,
                ownerId);
            
            throw;
        }
    }

    public Result ExecuteTransaction(
        DbWriteQueue.Context dbWriteContext, 
        StorageExtId storageExternalId, 
        int ownerId, 
        string name,
        Guid correlationId, 
        SqliteTransaction transaction)
    {
        var (workspaceExternalId, workspaceGuid) = WorkspaceExtId.NewIdWithSourceGuid();
        var finalBucketName = $"workspace-{workspaceGuid}";

        var insertWorkspaceResult = dbWriteContext
            .OneRowCmd(
                sql: @"
                     INSERT INTO w_workspaces(
                        w_external_id,
                        w_owner_id,
                        w_storage_id,
                        w_name,
                        w_current_size_in_bytes,
                        w_is_bucket_created,
                        w_bucket_name,
                        w_is_being_deleted
                    ) VALUES (
                        $externalId,
                        $userId,
                        (SELECT s_id FROM s_storages WHERE s_external_id = $storageExternalId LIMIT 1),
                        $name,
                        0,
                        FALSE,
                        $bucketName,
                        FALSE
                    )   
                    RETURNING 
                        w_id,
                        w_storage_id
                ",
                readRowFunc: reader => new
                {
                    WorkspaceId = reader.GetInt32(0),
                    StorageId = reader.GetInt32(1)
                },
                transaction: transaction)
            .WithParameter("$externalId", workspaceExternalId.Value)
            .WithParameter("$storageExternalId", storageExternalId.Value)
            .WithParameter("$userId", ownerId)
            .WithParameter("$name", name)
            .WithParameter("$bucketName", finalBucketName)
            .Execute();

        if (insertWorkspaceResult.IsEmpty)
        {
            throw new InvalidOperationException(
                $"Cannot insert workspace for OwnerId: {ownerId}");
        }

        var createBucketQueueJobResult = queue.Enqueue(
            correlationId: correlationId,
            jobType: CreateWorkspaceBucketQueueJobType.Value,
            definition: new CreateWorkspaceBucketQueueJobDefinition(
                WorkspaceId: insertWorkspaceResult.Value.WorkspaceId,
                BucketName: finalBucketName,
                StorageId: insertWorkspaceResult.Value.StorageId),
            debounceId: null,
            sagaId: null,
            executeAfterDate: clock.UtcNow,
            dbWriteContext: dbWriteContext,
            transaction: transaction);

        if (createBucketQueueJobResult.IsEmpty)
        {
            throw new InvalidOperationException(
                $"Cannot enqueue bucket creation job for new workspace for OwnerId: {ownerId}");
        }
        
        return new Result(
            Code: ResultCode.Ok,
            Workspace: new Workspace(
                Id: insertWorkspaceResult.Value.WorkspaceId,
                ExternalId: workspaceExternalId,
                BucketName: finalBucketName));
    }

    public enum ResultCode
    {
        Ok = 0,
        StorageNotFound
    }
    
    public readonly record struct Result(
        ResultCode Code,
        Workspace Workspace = default);
 
    public readonly record struct Workspace(
        int Id,
        WorkspaceExtId ExternalId,
        string BucketName);
}