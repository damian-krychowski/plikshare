using System.Text;
using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using PlikShare.Files.Created;
using PlikShare.Files.Id;
using PlikShare.Files.Metadata;
using PlikShare.Workspaces.Cache;
using Serilog;
using Serilog.Events;

namespace PlikShare.Uploads.CompleteFileUpload;

//todo handle file upload not found error
public class BulkConvertDirectFileUploadsToFilesQuery(
    IClock clock,
    DbWriteQueue dbWriteQueue,
    FileCreatedDispatcher fileCreatedDispatcher,
    EphemeralKeyRing ephemeralKeyRing)
{
    private static readonly Serilog.ILogger Logger = Log.ForContext<ConvertFileUploadToFileQuery>();

    public Task<ResultCode> Execute(
        WorkspaceContext workspace,
        WorkspaceEncryptionSession? workspaceEncryptionSession,
        int[] fileUploadIds,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                workspace: workspace,
                workspaceEncryptionSession: workspaceEncryptionSession,
                fileUploadIds: fileUploadIds,
                correlationId: correlationId),
            cancellationToken: cancellationToken);
    }

    private ResultCode ExecuteOperation(
        SqliteWriteContext dbWriteContext,
        WorkspaceContext workspace,
        WorkspaceEncryptionSession? workspaceEncryptionSession,
        int[] fileUploadIds,
        Guid correlationId)
    {
        using var transaction = dbWriteContext.Connection.BeginTransaction();

        try
        {
            Logger.Debug("Starting bulk conversion of file uploads to files. FileUploadIds: {FileUploadIds}",
                fileUploadIds);

            var insertedFiles = dbWriteContext
                .AggregateRows(
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
                             fu_file_external_id,
                             fu_workspace_id,
                             fu_folder_id,
                             fu_file_key_secret_part,
                             fu_file_name,
                             fu_file_extension,
                             fu_file_content_type,
                             fu_file_size_in_bytes,
                             TRUE,
                             fu_owner_identity_type,
                             fu_owner_identity,
                             $createdAt,
                             fu_encryption_key_version,
                             fu_encryption_salt,
                             fu_encryption_nonce_prefix,
                             fu_encryption_chain_salts,
                             fu_encryption_format_version,
                             fu_parent_file_id,
                             fu_file_metadata
                         FROM fu_file_uploads
                         WHERE fu_id IN (
                             SELECT value FROM json_each($fileUploadIds)
                         )
                         RETURNING
                            fi_parent_file_id,
                            fi_id,
                            fi_external_id,
                            fi_size_in_bytes,
                            fi_content_type,
                            fi_encryption_key_version,
                            fi_encryption_salt,
                            fi_encryption_nonce_prefix,
                            fi_encryption_chain_salts,
                            fi_encryption_format_version,
                            fi_uploader_identity_type,
                            fi_uploader_identity
                         """,
                    seed: new InsertedFilesAcc(
                        CreatedFiles: [],
                        Count: 0),
                    name: "upload.bulk_convert.insert_files",
                    aggregateRowFunc: (acc, reader) =>
                    {
                        if (reader.IsDBNull(0))
                        {
                            var encryptionMetadata = reader.GetByteOrNull(5) is { } keyVersion
                                ? new FileEncryptionMetadata
                                {
                                    KeyVersion = keyVersion,
                                    Salt = reader.GetFieldValue<byte[]>(6),
                                    NoncePrefix = reader.GetFieldValue<byte[]>(7),
                                    ChainStepSalts = KeyDerivationChain.Deserialize(
                                        reader.GetFieldValueOrNull<byte[]>(8)),
                                    FormatVersion = reader.GetByteOrNull(9) ?? 1
                                }
                                : null;

                            acc.CreatedFiles.Add(new CreatedFile(
                                Id: reader.GetInt32(1),
                                ExternalId: reader.GetExtId<FileExtId>(2),
                                SizeInBytes: reader.GetInt64(3),
                                ContentType: reader.GetEncodedMetadata(4),
                                UploaderIdentityType: reader.GetString(10),
                                UploaderIdentity: reader.GetString(11),
                                EncryptionMetadata: encryptionMetadata,
                                EncryptionSeed: workspace.TryGetFileEncryptionSeed(
                                    encryptionMetadata: encryptionMetadata,
                                    workspaceEncryptionSession: workspaceEncryptionSession,
                                    ephemeralKeyRing: ephemeralKeyRing)));
                        }

                        return acc with
                        {
                            Count = acc.Count + 1
                        };
                    },
                    transaction: transaction)
                .WithJsonParameter("$fileUploadIds", fileUploadIds)
                .WithParameter("$createdAt", clock.UtcNow)
                .Execute();

            if (insertedFiles.Count != fileUploadIds.Length)
            {
                throw new InvalidOperationException(
                    $"Failed to insert {fileUploadIds.Length - insertedFiles.Count} Files during bulk upload of FileUpload '{string.Join(", ", fileUploadIds)}'");
            }

            if (Logger.IsEnabled(LogEventLevel.Debug))
            {
                Logger.Debug("Successfully inserted files {FileIds}",
                    insertedFiles.CreatedFiles.Select(file => file.Id));
            }

            var deletedIds = dbWriteContext
                .Cmd(
                    sql: @"
                        DELETE FROM fu_file_uploads
                        WHERE fu_id IN (
                            SELECT value FROM json_each($fileUploadIds)
                        )
                        RETURNING fu_id",
                    readRowFunc: reader => reader.GetInt32(0),
                    transaction: transaction,
                    name: "upload.bulk_convert.delete_uploads")
                .WithJsonParameter("$fileUploadIds", fileUploadIds)
                .Execute();

            if (deletedIds.Count != fileUploadIds.Length)
            {
                //todo improve that log, tell expilicely which file uploads where not found
                Logger.Warning(
                    "Failed to delete {Count} file uploads. FileUploads  where not found",
                    fileUploadIds.Length - deletedIds.Count);
            }
            else
            {
                Logger.Debug(
                    "Successfully deleted file uploads. FileUploadIds: {FileUploadIds}",
                    fileUploadIds);
            }

            fileCreatedDispatcher.OnFilesCreated(new FileCreatedBatch(
                Workspace: workspace,
                Session: workspaceEncryptionSession,
                CorrelationId: correlationId,
                DbWriteContext: dbWriteContext,
                Transaction: transaction,
                Files: insertedFiles.CreatedFiles));

            transaction.Commit();

            Logger.Debug(
                "Successfully completed bulk file upload conversion.");

            return ResultCode.Ok;

        }
        catch (Exception ex)
        {
            transaction.Rollback();

            Logger.Error(ex,
                "Error in bulk conversion of file uploads. Rolling back transaction. FileUploadIds: {FileUploadIds}",
                fileUploadIds);

            throw;
        }
    }

    private readonly record struct InsertedFilesAcc(
        List<CreatedFile> CreatedFiles,
        int Count);

    public enum ResultCode
    {
        Ok = 0,
    }
}
