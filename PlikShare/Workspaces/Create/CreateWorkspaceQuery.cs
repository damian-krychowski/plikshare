using Microsoft.Data.Sqlite;
using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Queue;
using PlikShare.Core.SQLite;
using PlikShare.Storages.Id;
using PlikShare.Users.Cache;
using PlikShare.Workspaces.CreateBucket;
using PlikShare.Workspaces.Encryption;
using PlikShare.Workspaces.Id;
using Serilog;

namespace PlikShare.Workspaces.Create;

/// <summary>
/// Persists a new workspace row, enqueues the bucket-creation job, and (when the caller
/// supplies full-encryption artifacts) inserts the owner's wrap into
/// <c>wek_workspace_encryption_keys</c>. Performs no sealing, no key derivation, and holds
/// no plaintext DEK — all crypto is precomputed by <see cref="WorkspaceCreationPreparation"/>
/// and handed in via <see cref="WorkspaceFullEncryptionArtifacts"/>. Storage existence and
/// encryption-type checks are also the preparation component's job; this query trusts the
/// caller and lets FK violations bubble up as <see cref="ResultCode.StorageNotFound"/>.
/// </summary>
public class CreateWorkspaceQuery(
    PlikShareDb plikShareDb,
    IClock clock,
    IQueue queue,
    DbWriteQueue dbWriteQueue,
    UpsertWorkspaceEncryptionKeyQuery upsertWorkspaceEncryptionKeyQuery)
{
    public async Task<Result> Execute(
        StorageExtId storageExternalId,
        UserContext user,
        WorkspaceFullEncryptionArtifacts? artifacts,
        string name,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        if(user.MaxWorkspaceNumber.HasValue)
        {
            var currentNumber = GetCurrentNumberOfWorkspaces(
                ownerId: user.Id);

            if (currentNumber >= user.MaxWorkspaceNumber)
                return new Result(Code: ResultCode.MaxNumberOfWorkspacesReached);
        }

        return await dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                storageExternalId: storageExternalId,
                ownerId: user.Id,
                artifacts: artifacts,
                name: name,
                maxSizeInBytes: user.DefaultMaxWorkspaceSizeInBytes,
                maxTeamMembers: user.DefaultMaxWorkspaceTeamMembers,
                correlationId: correlationId),
            cancellationToken: cancellationToken);
    }

    private int GetCurrentNumberOfWorkspaces(int ownerId)
    {
        using var connection = plikShareDb.OpenConnection();

        return connection
            .OneRowCmd(
                sql: """
                SELECT COUNT(*)
                FROM w_workspaces
                WHERE w_owner_id = $ownerId
                    AND w_is_being_deleted = FALSE
                """,
                readRowFunc: reader => reader.GetInt32(0)
            )
            .WithParameter("$ownerId", ownerId)
            .ExecuteOrThrow();
    }

    private Result ExecuteOperation(
        SqliteWriteContext dbWriteContext,
        StorageExtId storageExternalId,
        int ownerId,
        WorkspaceFullEncryptionArtifacts? artifacts,
        string name,
        long? maxSizeInBytes,
        int? maxTeamMembers,
        Guid correlationId)
    {
        using var transaction = dbWriteContext.Connection.BeginTransaction();

        try
        {
            var result = ExecuteTransaction(
                dbWriteContext: dbWriteContext,
                storageExternalId: storageExternalId,
                ownerId: ownerId,
                artifacts: artifacts,
                name: name,
                maxSizeInBytes: maxSizeInBytes,
                maxTeamMembers: maxTeamMembers,
                correlationId: correlationId,
                transaction: transaction);

            if (result.Code != ResultCode.Ok)
            {
                transaction.Rollback();
                return result;
            }

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

    /// <summary>
    /// Writes the workspace row and (if provided) the owner's wrap in
    /// <c>wek_workspace_encryption_keys</c>. For a full-encrypted storage
    /// <paramref name="artifacts"/> MUST be non-null; for None/Managed it MUST be null.
    /// See <see cref="WorkspaceCreationPreparation"/> for the crypto side.
    /// </summary>
    public Result ExecuteTransaction(
        SqliteWriteContext dbWriteContext,
        StorageExtId storageExternalId,
        int ownerId,
        WorkspaceFullEncryptionArtifacts? artifacts,
        string name,
        long? maxSizeInBytes,
        int? maxTeamMembers,
        Guid correlationId,
        SqliteTransaction transaction)
    {
        var (workspaceExternalId, workspaceGuid) = WorkspaceExtId.NewIdWithSourceGuid();
        var finalBucketName = $"workspace-{workspaceGuid}";

        var insertWorkspaceResult = dbWriteContext
            .OneRowCmd(
                sql: """
                      INSERT INTO w_workspaces(
                         w_external_id,
                         w_owner_id,
                         w_storage_id,
                         w_name,
                         w_current_size_in_bytes,
                         w_is_bucket_created,
                         w_bucket_name,
                         w_is_being_deleted,
                         w_max_size_in_bytes,
                         w_max_team_members,
                         w_encryption_salt
                     ) VALUES (
                         $externalId,
                         $userId,
                         (SELECT s_id FROM s_storages WHERE s_external_id = $storageExternalId LIMIT 1),
                         $name,
                         0,
                         FALSE,
                         $bucketName,
                         FALSE,
                         $maxSizeInBytes,
                         $maxTeamMembers,
                         $encryptionSalt
                     )
                     RETURNING
                         w_id,
                         w_storage_id
                     """,
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
            .WithParameter("$maxSizeInBytes", maxSizeInBytes)
            .WithParameter("$maxTeamMembers", maxTeamMembers)
            .WithParameter("$encryptionSalt", (object?) artifacts?.EncryptionSalt ?? DBNull.Value)
            .Execute();

        if (insertWorkspaceResult.IsEmpty)
        {
            throw new InvalidOperationException(
                $"Cannot insert workspace for OwnerId: {ownerId}");
        }

        if (artifacts is not null)
        {
            // The owner is implicitly the first workspace member; without this wrap
            // they would be unable to read their own newly-created workspace's files.
            // The wrap is tied to the Storage DEK version that the Workspace DEK was
            // derived from, so files written under this version can later be lined up
            // with the correct wrap at read time.
            upsertWorkspaceEncryptionKeyQuery.ExecuteTransaction(
                dbWriteContext: dbWriteContext,
                workspaceId: insertWorkspaceResult.Value.WorkspaceId,
                userId: ownerId,
                storageDekVersion: artifacts.StorageDekVersion,
                wrappedWorkspaceDek: artifacts.OwnerWrappedWorkspaceDek,
                wrappedByUserId: ownerId,
                transaction: transaction);
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
                BucketName: finalBucketName,
                MaxSizeInBytes: maxSizeInBytes));
    }

    public enum ResultCode
    {
        Ok = 0,
        StorageNotFound,
        MaxNumberOfWorkspacesReached
    }

    public readonly record struct Result(
        ResultCode Code,
        Workspace Workspace = default);

    public readonly record struct Workspace(
        int Id,
        WorkspaceExtId ExternalId,
        string BucketName,
        long? MaxSizeInBytes);
}
