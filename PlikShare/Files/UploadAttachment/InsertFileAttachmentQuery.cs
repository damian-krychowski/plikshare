using Microsoft.Data.Sqlite;
using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Core.UserIdentity;
using PlikShare.Files.Id;
using PlikShare.Storages;
using PlikShare.Workspaces.Cache;
using Serilog;

namespace PlikShare.Files.UploadAttachment;

public class InsertFileAttachmentQuery(
    DbWriteQueue dbWriteQueue,
    IClock clock)
{
    public Task<ResultCode> Execute(
        WorkspaceContext workspace,
        FileExtId parentFileExternalId,
        AttachmentFile attachment,
        IUserIdentity uploader,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                workspace: workspace,
                parentFileExternalId: parentFileExternalId,
                attachment: attachment,
                uploader: uploader),
            cancellationToken: cancellationToken);
    }

    private ResultCode ExecuteOperation(
        DbWriteQueue.Context dbWriteContext,
        WorkspaceContext workspace,
        FileExtId parentFileExternalId,
        AttachmentFile attachment,
        IUserIdentity uploader)
    {
        var transaction = dbWriteContext.Connection.BeginTransaction();

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
                         )
                         INSERT INTO fi_files (
                            fi_external_id,
                            fi_workspace_id,
                            fi_folder_id,
                            fi_s3_key_secret_part,
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
                            fi_parent_file_id,
                            fi_metadata
                         )
                         VALUES (
                            $externalId,
                            $workspaceId,
                            (SELECT fi_folder_id FROM parent),
                            $s3KeySecretPart,
                            $name,
                            $extension,
                            $contentType,
                            $sizeInBytes,
                            FALSE,
                            $uploaderIdentityType,
                            $uploaderIdentity,
                            $createdAt,
                            $encryptionKeyVersion,
                            $encryptionSalt,
                            $encryptionNoncePrefix,
                            (SELECT fi_id FROM parent),
                            NULL
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
                .WithParameter("$s3KeySecretPart", attachment.S3KeySecretPart)
                .WithParameter("$name", attachment.Name)
                .WithParameter("$extension", attachment.Extension)
                .WithParameter("$contentType", attachment.ContentType)
                .WithParameter("$sizeInBytes", attachment.SizeInBytes)
                .WithParameter("$uploaderIdentityType", uploader.IdentityType)
                .WithParameter("$uploaderIdentity", uploader.Identity)
                .WithParameter("$createdAt", clock.UtcNow)
                .WithParameter("$encryptionKeyVersion", attachment.Encryption.Metadata?.KeyVersion)
                .WithParameter("$encryptionSalt", attachment.Encryption.Metadata?.Salt)
                .WithParameter("$encryptionNoncePrefix", attachment.Encryption.Metadata?.NoncePrefix)
                .ExecuteOrThrow();

            if (result.ParentId is null)
            {
                transaction.Rollback();

                Log.Warning(
                    "Parent file not found during attachment insert. AttachmentFileExternalId='{AttachmentFileExternalId}', ParentFileExternalId='{ParentFileExternalId}', WorkspaceExternalId='{WorkspaceExternalId}'",
                    attachment.ExternalId,
                    parentFileExternalId,
                    workspace.ExternalId);

                return ResultCode.ParentFileNotFound;
            }

            transaction.Commit();

            Log.Information(
                "Successfully inserted file attachment. AttachmentFileId='{AttachmentFileId}', AttachmentFileExternalId='{AttachmentFileExternalId}', ParentFileId='{ParentFileId}', WorkspaceExternalId='{WorkspaceExternalId}'",
                result.Id,
                attachment.ExternalId,
                result.ParentId,
                workspace.ExternalId);

            return ResultCode.Ok;
        }
        catch (SqliteException ex) when (ex.HasForeignKeyFailed())
        {
            transaction.Rollback();

            Log.Error(ex,
                "Foreign Key constraint failed while saving File Attachment. AttachmentFileExternalId='{AttachmentFileExternalId}', ParentFileExternalId='{ParentFileExternalId}', WorkspaceExternalId='{WorkspaceExternalId}'",
                attachment.ExternalId,
                parentFileExternalId,
                workspace.ExternalId);

            return ResultCode.ParentFileNotFound;
        }
        catch (Exception ex)
        {
            transaction.Rollback();

            Log.Error(ex,
                "Unexpected error occurred while saving File Attachment. AttachmentFileExternalId='{AttachmentFileExternalId}', ParentFileExternalId='{ParentFileExternalId}', WorkspaceExternalId='{WorkspaceExternalId}'",
                attachment.ExternalId,
                parentFileExternalId,
                workspace.ExternalId);

            throw;
        }
    }

    public enum ResultCode
    {
        Ok = 0,
        ParentFileNotFound
    }

    public class AttachmentFile
    {
        public required FileExtId ExternalId { get; init; }
        public required string Name { get; init; }
        public required string Extension { get; init; }
        public required string ContentType { get; init; }
        public required string S3KeySecretPart { get; init; }
        public required long SizeInBytes { get; init; }
        public required FileEncryption Encryption { get; init; }
    }
}