using Microsoft.Data.Sqlite;
using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using PlikShare.Core.UserIdentity;
using PlikShare.Files.Delete;
using PlikShare.Files.Id;
using PlikShare.Files.UploadAttachment;
using PlikShare.Workspaces.Cache;
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
            // Insert as COMPLETED with metadata set in one shot — no second pass to flip the
            // flag and write metadata, which is the whole point of this query.
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
                            $metadata
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
                    transaction: transaction)
                .WithParameter("$parentExternalId", parentFileExternalId.Value)
                .WithParameter("$workspaceId", workspace.Id)
                .WithParameter("$externalId", attachment.ExternalId.Value)
                .WithParameter("$keySecretPart", attachment.KeySecretPart)
                .WithEncryptableParameter("$name", attachment.Name)
                .WithEncryptableParameter("$extension", attachment.Extension)
                .WithEncryptableParameter("$contentType", attachment.ContentType)
                .WithParameter("$sizeInBytes", attachment.SizeInBytes)
                .WithParameter("$uploaderIdentityType", uploader.IdentityType)
                .WithParameter("$uploaderIdentity", uploader.Identity)
                .WithParameter("$createdAt", clock.UtcNow)
                .WithParameter("$encryptionKeyVersion", attachment.EncryptionMetadata?.KeyVersion)
                .WithParameter("$encryptionSalt", attachment.EncryptionMetadata?.Salt)
                .WithParameter("$encryptionNoncePrefix", attachment.EncryptionMetadata?.NoncePrefix)
                .WithParameter("$encryptionChainSalts", KeyDerivationChain.Serialize(attachment.EncryptionMetadata?.ChainStepSalts))
                .WithParameter("$encryptionFormatVersion", attachment.EncryptionMetadata?.FormatVersion)
                .WithEncryptableBlobParameterOrNull("$metadata", attachment.Metadata)
                .ExecuteOrThrow();

            if (result.ParentId is null)
            {
                transaction.Rollback();

                Log.Warning(
                    "Parent file not found during thumbnail insert. ThumbnailExternalId='{ThumbnailExternalId}', ParentFileExternalId='{ParentFileExternalId}', WorkspaceExternalId='{WorkspaceExternalId}'",
                    attachment.ExternalId,
                    parentFileExternalId,
                    workspace.ExternalId);

                return ResultCode.ParentNotFound;
            }

            hardDeleteFilesWithStorageCleanupSubQuery.Execute(
                workspaceId: workspace.Id,
                fileIds: oldThumbnailFileIds,
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

    public enum ResultCode
    {
        Ok = 0,
        ParentNotFound
    }
}
