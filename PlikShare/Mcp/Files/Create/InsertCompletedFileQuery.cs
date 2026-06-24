using Microsoft.Data.Sqlite;
using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using PlikShare.Core.UserIdentity;
using PlikShare.Files.Id;
using PlikShare.Workspaces.Cache;
using Serilog;

namespace PlikShare.Mcp.Files.Create;

public class InsertCompletedFileQuery(
    DbWriteQueue dbWriteQueue,
    IClock clock)
{
    public Task<ResultCode> Execute(
        WorkspaceContext workspace,
        int? folderId,
        NewFile file,
        IUserIdentity uploader,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                workspace: workspace,
                folderId: folderId,
                file: file,
                uploader: uploader),
            cancellationToken: cancellationToken);
    }

    private ResultCode ExecuteOperation(
        SqliteWriteContext dbWriteContext,
        WorkspaceContext workspace,
        int? folderId,
        NewFile file,
        IUserIdentity uploader)
    {
        try
        {
            dbWriteContext
                .OneRowCmd(
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
                         VALUES (
                             $externalId,
                             $workspaceId,
                             $folderId,
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
                             NULL,
                             NULL
                         )
                         RETURNING fi_id
                         """,
                    readRowFunc: reader => reader.GetInt32(0))
                .WithParameter("$externalId", file.ExternalId.Value)
                .WithParameter("$workspaceId", workspace.Id)
                .WithParameter("$folderId", folderId)
                .WithParameter("$keySecretPart", file.KeySecretPart)
                .WithParameter("$name", file.Name)
                .WithParameter("$extension", file.Extension)
                .WithParameter("$contentType", file.ContentType)
                .WithParameter("$sizeInBytes", file.SizeInBytes)
                .WithParameter("$uploaderIdentityType", uploader.IdentityType)
                .WithParameter("$uploaderIdentity", uploader.Identity)
                .WithParameter("$createdAt", clock.UtcNow)
                .WithParameter("$encryptionKeyVersion", file.EncryptionMetadata?.KeyVersion)
                .WithParameter("$encryptionSalt", file.EncryptionMetadata?.Salt)
                .WithParameter("$encryptionNoncePrefix", file.EncryptionMetadata?.NoncePrefix)
                .WithParameter("$encryptionChainSalts", KeyDerivationChain.Serialize(file.EncryptionMetadata?.ChainStepSalts))
                .WithParameter("$encryptionFormatVersion", file.EncryptionMetadata?.FormatVersion)
                .ExecuteOrThrow();

            return ResultCode.Ok;
        }
        catch (SqliteException ex) when (ex.HasForeignKeyFailed())
        {
            Log.Warning(
                "Could not create file '{FileExternalId}' in workspace '{WorkspaceExternalId}' - target folder not found.",
                file.ExternalId,
                workspace.ExternalId);

            return ResultCode.FolderNotFound;
        }
    }

    public enum ResultCode
    {
        Ok = 0,
        FolderNotFound
    }

    public class NewFile
    {
        public required FileExtId ExternalId { get; init; }
        public required string KeySecretPart { get; init; }
        public required EncodedMetadataValue Name { get; init; }
        public required EncodedMetadataValue Extension { get; init; }
        public required EncodedMetadataValue ContentType { get; init; }
        public required long SizeInBytes { get; init; }
        public required FileEncryptionMetadata? EncryptionMetadata { get; init; }
    }
}
