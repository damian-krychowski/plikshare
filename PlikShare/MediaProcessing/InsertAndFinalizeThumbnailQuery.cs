using Microsoft.Data.Sqlite;
using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.Queue;
using PlikShare.Core.SQLite;
using PlikShare.Core.UserIdentity;
using PlikShare.Files.Delete;
using PlikShare.Files.Id;
using PlikShare.Files.UploadAttachment;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.UpdateCurrentSizeInBytes.QueueJob;
using Serilog;

namespace PlikShare.MediaProcessing;

/// <summary>
/// One-transaction insert + finalize for a thumbnail attachment whose bytes have ALREADY been
/// written to storage. Replaces the previous two-step <c>InsertFileAttachmentQuery</c>
/// (incomplete row) + <c>FinalizeThumbnailUploadQuery</c> (update metadata + mark uploaded +
/// hard-delete old) — two trips through <c>DbWriteQueue</c> collapsed into one.
///
/// Trade-off: a crash between storage upload and this query leaves an orphan blob on disk
/// instead of an orphan DB row. Acceptable for thumbnail bytes (small) and can be reaped later
/// by a separate GC job if it becomes a real problem.
/// </summary>
public class InsertAndFinalizeThumbnailQuery(
    DbWriteQueue dbWriteQueue,
    IClock clock,
    IQueue queue,
    HardDeleteFilesWithStorageCleanupSubQuery hardDeleteFilesWithStorageCleanupSubQuery)
{
    public Task<ResultCode> Execute(
        WorkspaceContext workspace,
        FileExtId parentFileExternalId,
        InsertFileAttachmentQuery.AttachmentFile attachment,
        List<int> oldThumbnailFileIds,
        IUserIdentity uploader,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                workspace: workspace,
                parentFileExternalId: parentFileExternalId,
                attachment: attachment,
                oldThumbnailFileIds: oldThumbnailFileIds,
                uploader: uploader,
                correlationId: correlationId),
            cancellationToken: cancellationToken);
    }

    public Task ExecuteBatch(
        WorkspaceContext workspace,
        IUserIdentity uploader,
        List<BulkInsertFileEntity> items,
        List<int> allOldThumbnailIds,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        if (items.Count == 0)
            return Task.CompletedTask;
        
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteBatchOperation(
                dbWriteContext: context,
                workspace: workspace,
                uploader: uploader,
                files: items,
                allOldThumbnailIds: allOldThumbnailIds,
                correlationId: correlationId),
            cancellationToken: cancellationToken);
    }

    private void ExecuteBatchOperation(
        SqliteWriteContext dbWriteContext,
        WorkspaceContext workspace,
        IUserIdentity uploader,
        List<BulkInsertFileEntity> files,
        List<int> allOldThumbnailIds,
        Guid correlationId)
    {
        dbWriteContext.Connection.RegisterJsonArrayToBlobFunction();
        var transaction = dbWriteContext.Connection.BeginTransaction();

        try
        {
            InsertBulk(
                dbWriteContext: dbWriteContext,
                workspace: workspace,
                uploader: uploader,
                fileEntities: files,
                transaction: transaction);

            hardDeleteFilesWithStorageCleanupSubQuery.Execute(
                workspaceId: workspace.Id,
                fileIds: allOldThumbnailIds,
                correlationId: correlationId,
                dbWriteContext: dbWriteContext,
                transaction: transaction);

            queue.EnqueueWorkspaceSizeUpdateJob(
                clock: clock,
                workspaceId: workspace.Id,
                correlationId: correlationId,
                dbWriteContext: dbWriteContext,
                transaction: transaction);

            transaction.Commit();

            Log.Information(
                "Batched insert+finalize: {InsertedCount} thumbnails ({BatchSize} items), {OldCount} old thumbnails replaced in Workspace#{WorkspaceId}.",
                files.Count,
                files.Count,
                allOldThumbnailIds.Count,
                workspace.Id);
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private void InsertBulk(
        SqliteWriteContext dbWriteContext,
        WorkspaceContext workspace,
        IUserIdentity uploader,
        List<BulkInsertFileEntity> fileEntities,
        SqliteTransaction transaction)
    {
        var results = dbWriteContext
            .Cmd(
                sql: """
                     INSERT INTO fi_files (
                        fi_external_id,
                        fi_workspace_id,
                        fi_folder_id,
                        fi_key_secret_part,
                        fi_name,
                        fi_extension,
                        fi_content_type,
                        fi_size_in_bytes,
                        fi_is_upload_completed,
                        fi_uploader_identity_type,
                        fi_uploader_identity,
                        fi_created_at,
                        fi_encryption_key_version,
                        fi_encryption_salt,
                        fi_encryption_nonce_prefix,
                        fi_encryption_chain_salts,
                        fi_encryption_format_version,
                        fi_parent_file_id,
                        fi_metadata
                     )
                     SELECT 
                        json_extract(value, '$.fileExternalId'),
                        $workspaceId,
                        json_extract(value, '$.folderId'),
                        json_extract(value, '$.keySecretPart'),
                        json_extract(value, '$.fileName'),
                        json_extract(value, '$.fileExtension'),
                        json_extract(value, '$.fileContentType'),
                        json_extract(value, '$.fileSizeInBytes'),
                        TRUE,
                        $uploaderIdentityType,
                        $uploaderIdentity,
                        $createdAt,
                        json_extract(value, '$.encryptionKeyVersion'),
                        app_json_array_to_blob(json_extract(value, '$.encryptionSalt')),
                        app_json_array_to_blob(json_extract(value, '$.encryptionNoncePrefix')),
                        app_json_array_to_blob(json_extract(value, '$.encryptionChainSalts')),
                        json_extract(value, '$.encryptionFormatVersion'),
                        json_extract(value, '$.parentFileId'),
                        CAST(json_extract(value, '$.fileMetadata') AS BLOB)
                     FROM
                        json_each($files)
                     RETURNING
                        fi_external_id
                     """,
                readRowFunc: reader => reader.GetString(0),
                transaction: transaction,
                name: "media.thumbnail.insert_bulk")
            .WithJsonParameter("$files", fileEntities)
            .WithParameter("$workspaceId", workspace.Id)
            .WithParameter("$uploaderIdentityType", uploader.IdentityType)
            .WithParameter("$uploaderIdentity", uploader.Identity)
            .WithParameter("$createdAt", clock.UtcNow)
            .Execute();

        if (results.Count != fileEntities.Count)
        {
            throw new InvalidOperationException(
                $"Something went wrong while inserting File thumbnails. " +
                $"Expected uploads: '{string.Join(", ", fileEntities.Select(x => x.FileExternalId))}' " +
                $"Inserted uploads: '{string.Join(", ", results)}'");
        }
    }
    
    public class BulkInsertFileEntity
    {
        public required string FileExternalId { get; init; }
        public required int? FolderId { get; init; }
        public required string KeySecretPart { get; init; }
        public required EncodedMetadataValue FileName { get; init; }
        public required EncodedMetadataValue FileExtension { get; init; }
        public required EncodedMetadataValue FileContentType { get; init; }
        public required long FileSizeInBytes { get; init; }
        public required int ParentFileId { get; init; }
        public required EncodedMetadataValue? FileMetadata { get; init; }

        public required byte? EncryptionKeyVersion { get; init; }
        public required byte[]? EncryptionSalt { get; init; }
        public required byte[]? EncryptionNoncePrefix { get; init; }
        public required byte[]? EncryptionChainSalts { get; init; }
        public required byte? EncryptionFormatVersion { get; init; }
    }

    private ResultCode ExecuteOperation(
        SqliteWriteContext dbWriteContext,
        WorkspaceContext workspace,
        FileExtId parentFileExternalId,
        InsertFileAttachmentQuery.AttachmentFile attachment,
        List<int> oldThumbnailFileIds,
        IUserIdentity uploader,
        Guid correlationId)
    {
        var transaction = dbWriteContext.Connection.BeginTransaction();

        try
        {
            var rc = InsertOne(
                dbWriteContext: dbWriteContext,
                transaction: transaction,
                workspace: workspace,
                parentFileExternalId: parentFileExternalId,
                attachment: attachment,
                uploader: uploader);

            if (rc != ResultCode.Ok)
            {
                transaction.Rollback();
                return rc;
            }

            hardDeleteFilesWithStorageCleanupSubQuery.Execute(
                workspaceId: workspace.Id,
                fileIds: oldThumbnailFileIds,
                correlationId: correlationId,
                dbWriteContext: dbWriteContext,
                transaction: transaction);

            // The new thumbnail is a child fi_files row counting toward the workspace size; recalc
            // it (debounced per workspace) so the cached w_current_size_in_bytes reflects the byte.
            queue.EnqueueWorkspaceSizeUpdateJob(
                clock: clock,
                workspaceId: workspace.Id,
                correlationId: correlationId,
                dbWriteContext: dbWriteContext,
                transaction: transaction);

            transaction.Commit();

            Log.Information(
                "Inserted+finalized thumbnail '{ThumbnailExternalId}'. Replaced {OldCount} old thumbnails in Workspace#{WorkspaceId}.",
                attachment.ExternalId,
                oldThumbnailFileIds.Count,
                workspace.Id);

            return ResultCode.Ok;
        }
        catch (SqliteException ex) when (ex.HasForeignKeyFailed())
        {
            transaction.Rollback();

            Log.Error(
                ex,
                "Foreign Key constraint failed while inserting thumbnail. ThumbnailExternalId='{ThumbnailExternalId}', ParentFileExternalId='{ParentFileExternalId}', WorkspaceExternalId='{WorkspaceExternalId}'",
                attachment.ExternalId,
                parentFileExternalId,
                workspace.ExternalId);

            return ResultCode.ParentNotFound;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private ResultCode InsertOne(
        SqliteWriteContext dbWriteContext,
        SqliteTransaction transaction,
        WorkspaceContext workspace,
        FileExtId parentFileExternalId,
        InsertFileAttachmentQuery.AttachmentFile attachment,
        IUserIdentity uploader)
    {
        try
        {
            var result = dbWriteContext
                .OneRowCmd(
                    sql: """
                         WITH parent AS (
                            SELECT
                                fi_id,
                                fi_folder_id
                            FROM fi_files
                            WHERE
                                fi_external_id = $parentExternalId
                                AND fi_workspace_id = $workspaceId
                                AND fi_deleted_at IS NULL
                         )
                         INSERT INTO fi_files (
                            fi_external_id,
                            fi_workspace_id,
                            fi_folder_id,
                            fi_key_secret_part,
                            fi_name,
                            fi_extension,
                            fi_content_type,
                            fi_size_in_bytes,
                            fi_is_upload_completed,
                            fi_uploader_identity_type,
                            fi_uploader_identity,
                            fi_created_at,
                            fi_encryption_key_version,
                            fi_encryption_salt,
                            fi_encryption_nonce_prefix,
                            fi_encryption_chain_salts,
                            fi_encryption_format_version,
                            fi_parent_file_id,
                            fi_metadata
                         )
                         VALUES (
                            $externalId,
                            $workspaceId,
                            (SELECT fi_folder_id FROM parent),
                            $keySecretPart,
                            $name,
                            $extension,
                            $contentType,
                            $sizeInBytes,
                            TRUE,
                            $uploaderIdentityType,
                            $uploaderIdentity,
                            $createdAt,
                            $encryptionKeyVersion,
                            $encryptionSalt,
                            $encryptionNoncePrefix,
                            $encryptionChainSalts,
                            $encryptionFormatVersion,
                            (SELECT fi_id FROM parent),
                            CAST($metadata AS BLOB)
                         )
                         RETURNING
                            fi_id,
                            fi_parent_file_id
                         """,
                    readRowFunc: reader => new
                    {
                        Id = reader.GetInt32(0),
                        ParentId = reader.GetInt32OrNull(1)
                    },
                    transaction: transaction,
                    name: "media.thumbnail.insert_one")
                .WithParameter("$parentExternalId", parentFileExternalId.Value)
                .WithParameter("$workspaceId", workspace.Id)
                .WithParameter("$externalId", attachment.ExternalId.Value)
                .WithParameter("$keySecretPart", attachment.KeySecretPart)
                .WithParameter("$name", attachment.Name)
                .WithParameter("$extension", attachment.Extension)
                .WithParameter("$contentType", attachment.ContentType)
                .WithParameter("$sizeInBytes", attachment.SizeInBytes)
                .WithParameter("$uploaderIdentityType", uploader.IdentityType)
                .WithParameter("$uploaderIdentity", uploader.Identity)
                .WithParameter("$createdAt", clock.UtcNow)
                .WithParameter("$encryptionKeyVersion", attachment.EncryptionMetadata?.KeyVersion)
                .WithParameter("$encryptionSalt", attachment.EncryptionMetadata?.Salt)
                .WithParameter("$encryptionNoncePrefix", attachment.EncryptionMetadata?.NoncePrefix)
                .WithParameter("$encryptionChainSalts", KeyDerivationChain.Serialize(attachment.EncryptionMetadata?.ChainStepSalts))
                .WithParameter("$encryptionFormatVersion", attachment.EncryptionMetadata?.FormatVersion)
                .WithParameter("$metadata", attachment.Metadata)
                .ExecuteOrThrow();

            if (result.ParentId is null)
            {
                Log.Warning(
                    "Parent file not found during thumbnail insert. ThumbnailExternalId='{ThumbnailExternalId}', ParentFileExternalId='{ParentFileExternalId}', WorkspaceExternalId='{WorkspaceExternalId}'",
                    attachment.ExternalId,
                    parentFileExternalId,
                    workspace.ExternalId);

                return ResultCode.ParentNotFound;
            }

            return ResultCode.Ok;
        }
        catch (SqliteException ex) when (ex.HasForeignKeyFailed())
        {
            Log.Error(
                ex,
                "Foreign Key constraint failed while inserting thumbnail. ThumbnailExternalId='{ThumbnailExternalId}', ParentFileExternalId='{ParentFileExternalId}', WorkspaceExternalId='{WorkspaceExternalId}'",
                attachment.ExternalId,
                parentFileExternalId,
                workspace.ExternalId);

            return ResultCode.ParentNotFound;
        }
    }

    public enum ResultCode
    {
        Ok = 0,
        ParentNotFound
    }
}
