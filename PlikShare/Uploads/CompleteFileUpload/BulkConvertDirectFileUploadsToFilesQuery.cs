using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Queue;
using PlikShare.Core.SQLite;
using PlikShare.Workspaces.UpdateCurrentSizeInBytes.QueueJob;
using Serilog;

namespace PlikShare.Uploads.CompleteFileUpload;

//todo handle file upload not found error
public class BulkConvertDirectFileUploadsToFilesQuery(
    IQueue queue,
    IClock clock,
    DbWriteQueue dbWriteQueue)
{
    private static readonly Serilog.ILogger Logger = Log.ForContext<ConvertFileUploadToFileQuery>();

    public Task<ResultCode> Execute(
        int[] fileUploadIds,
        int workspaceId,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                fileUploadIds: fileUploadIds,
                workspaceId: workspaceId,
                correlationId: correlationId),
            cancellationToken: cancellationToken);
    }

    private ResultCode ExecuteOperation(
        DbWriteQueue.Context dbWriteContext,
        int[] fileUploadIds,
        int workspaceId,
        Guid correlationId)
    {
        using var transaction = dbWriteContext.Connection.BeginTransaction();

        try
        {
            Logger.Debug("Starting bulk conversion of file uploads to files. FileUploadIds: {FileUploadIds}",
                fileUploadIds);

            var fileIds = dbWriteContext
                .Cmd(
                    sql: @"
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
                        SELECT
                            fu_file_external_id,
                            fu_workspace_id,
                            fu_folder_id,
                            fu_file_s3_key_secret_part,
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
                            fu_parent_file_id,
                            fu_file_metadata
                        FROM fu_file_uploads
                        WHERE fu_id IN (
                            SELECT value FROM json_each($fileUploadIds)
                        )
                        RETURNING fi_id",
                    readRowFunc: reader => reader.GetInt32(0),
                    transaction: transaction)
                .WithJsonParameter("$fileUploadIds", fileUploadIds)
                .WithParameter("$createdAt", clock.UtcNow)
                .Execute();

            if (fileIds.Count != fileUploadIds.Length)
            {
                throw new InvalidOperationException(
                    $"Failed to insert {fileUploadIds.Length - fileIds.Count} Files during bulk upload of FileUpload '{string.Join(", ", fileUploadIds)}'");
            }

            Logger.Debug("Successfully inserted files {FileIds}", fileIds);

            var deletedIds = dbWriteContext
                .Cmd(
                    sql: @"
                        DELETE FROM fu_file_uploads
                        WHERE fu_id IN (
                            SELECT value FROM json_each($fileUploadIds)
                        )
                        RETURNING fu_id",
                    readRowFunc: reader => reader.GetInt32(0),
                    transaction: transaction)
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
    
    public enum ResultCode
    {
        Ok = 0,
    }
}