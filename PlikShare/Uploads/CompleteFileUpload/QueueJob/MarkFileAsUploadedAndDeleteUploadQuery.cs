using Microsoft.Data.Sqlite;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using PlikShare.Files.Created;
using PlikShare.Files.Id;
using PlikShare.Workspaces.Cache;
using Serilog;

namespace PlikShare.Uploads.CompleteFileUpload.QueueJob;

public class MarkFileAsUploadedAndDeleteUploadQuery(
    FileCreatedDispatcher fileCreatedDispatcher)
{
    private static readonly Serilog.ILogger Logger = Log.ForContext<MarkFileAsUploadedAndDeleteUploadQuery>();

    public void Execute(
        SqliteWriteContext dbWriteContext,
        SqliteTransaction transaction,
        WorkspaceContext workspace,
        int fileUploadId,
        FileExtId fileExternalId,
        FullEncryptionSeedEphemeral? encryptionSeed,
        Guid correlationId)
    {
        Logger.Debug(
            "Starting to mark file as uploaded and delete file upload. FileUploadId: {FileUploadId}, FileExternalId: {FileExternalId}",
            fileUploadId,
            fileExternalId);

        var markedFile = dbWriteContext
            .OneRowCmd(
                sql: @"
                    UPDATE fi_files
                    SET fi_is_upload_completed = TRUE
                    WHERE fi_external_id = $fileExternalId
                    RETURNING
                        fi_id,
                        fi_size_in_bytes,
                        fi_content_type,
                        fi_encryption_key_version,
                        fi_encryption_salt,
                        fi_encryption_nonce_prefix,
                        fi_encryption_chain_salts,
                        fi_encryption_format_version,
                        fi_uploader_identity_type,
                        fi_uploader_identity",
                readRowFunc: reader => new CreatedFile(
                    Id: reader.GetInt32(0),
                    ExternalId: fileExternalId,
                    SizeInBytes: reader.GetInt64(1),
                    ContentType: reader.GetEncodedMetadata(2),
                    UploaderIdentityType: reader.GetString(8),
                    UploaderIdentity: reader.GetString(9),
                    EncryptionMetadata: reader.GetByteOrNull(3) is { } keyVersion
                        ? new FileEncryptionMetadata
                        {
                            KeyVersion = keyVersion,
                            Salt = reader.GetFieldValue<byte[]>(4),
                            NoncePrefix = reader.GetFieldValue<byte[]>(5),
                            ChainStepSalts = KeyDerivationChain.Deserialize(
                                reader.GetFieldValueOrNull<byte[]>(6)),
                            FormatVersion = reader.GetByteOrNull(7) ?? 1
                        }
                        : null,
                    EncryptionSeed: encryptionSeed),
                transaction: transaction,
                name: "upload.mark_uploaded.mark_file")
            .WithParameter("$fileExternalId", fileExternalId.Value)
            .Execute();

        if (markedFile.IsEmpty)
        {
            Logger.Warning(
                "Failed to update file upload status. File not found. FileUploadId: {FileUploadId}, FileExternalId: {FileExternalId}",
                fileUploadId,
                fileExternalId.Value);
        }
        else
        {
            Logger.Debug(
                "Successfully marked file as uploaded. FileId: {FileId}, FileExternalId: {FileExternalId}",
                markedFile.Value.Id,
                fileExternalId.Value);
        }

        var deletedParts = dbWriteContext
            .Cmd(
                sql: @"
                    DELETE FROM fup_file_upload_parts
                    WHERE fup_file_upload_id = $fileUploadId
                    RETURNING fup_part_number",
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction,
                name: "upload.mark_uploaded.delete_parts")
            .WithParameter("$fileUploadId", fileUploadId)
            .Execute()
            .ToList();

        Logger.Debug(
            "Deleted file upload parts. FileUploadId: {FileUploadId}, DeletedPartCount: {DeletedPartCount}, DeletedParts: {@DeletedParts}",
            fileUploadId,
            deletedParts.Count,
            deletedParts);

        var deletedUploadId = dbWriteContext
            .OneRowCmd(
                sql: @"
                    DELETE FROM fu_file_uploads
                    WHERE fu_id = $fileUploadId
                    RETURNING fu_id",
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction,
                name: "upload.mark_uploaded.delete_upload")
            .WithParameter("$fileUploadId", fileUploadId)
            .Execute();

        if (deletedUploadId.IsEmpty)
        {
            Logger.Warning(
                "Failed to delete file upload. FileUpload not found. FileUploadId: {FileUploadId}",
                fileUploadId);
        }
        else
        {
            Logger.Debug(
                "Successfully deleted file upload. FileUploadId: {FileUploadId}",
                fileUploadId);
        }

        if (!markedFile.IsEmpty)
        {
            fileCreatedDispatcher.OnFilesCreated(new FileCreatedBatch(
                Workspace: workspace,
                Session: null,
                CorrelationId: correlationId,
                DbWriteContext: dbWriteContext,
                Transaction: transaction,
                Files: [markedFile.Value]));
        }

        Logger.Information(
            "Successfully completed marking file as uploaded and deleting file upload. FileUploadId: {FileUploadId}, FileExternalId: {FileExternalId}",
            fileUploadId,
            fileExternalId.Value);
    }
}
