using Microsoft.Data.Sqlite;
using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Queue;
using PlikShare.Core.SQLite;
using PlikShare.Uploads.Algorithm;
using PlikShare.Uploads.Chunking;
using PlikShare.Uploads.CompleteFileUpload.QueueJob;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.UpdateCurrentSizeInBytes.QueueJob;
using Serilog;

namespace PlikShare.Uploads.CompleteFileUpload;

//todo is this class ever used in its original shape? i think direct uploads are not processed through here
public class ConvertFileUploadToFileQuery(
    IQueue queue,
    IClock clock,
    DbWriteQueue dbWriteQueue)
{
    private static readonly Serilog.ILogger Logger = Log.ForContext<ConvertFileUploadToFileQuery>();

    public Task<Result> Execute(
        FileUpload fileUpload,
        WorkspaceContext workspace,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                fileUpload: fileUpload,
                workspace: workspace,
                correlationId: correlationId),
            cancellationToken: cancellationToken);
    }

    private Result ExecuteOperation(
        DbWriteQueue.Context dbWriteContext,
        FileUpload fileUpload,
        WorkspaceContext workspace,
        Guid correlationId)
    {
        using var transaction = dbWriteContext.Connection.BeginTransaction();

        try
        {
            Logger.Debug("Starting conversion of FileUpload#{FileUploadId} to file.",
                fileUpload.Id);

            var result = fileUpload.UploadAlgorithm switch
            {
                UploadAlgorithm.DirectUpload => HandleConversionForDirectUploads(
                    dbWriteContext: dbWriteContext,
                    fileUpload: fileUpload,
                    transaction: transaction),

                UploadAlgorithm.SingleChunkUpload => HandleConversionForSingleChunkUploads(
                    dbWriteContext: dbWriteContext,
                    fileUpload: fileUpload,
                    transaction: transaction),

                UploadAlgorithm.MultiStepChunkUpload => HandleConversionForMultiStepChunkUploads(
                    dbWriteContext: dbWriteContext,
                    fileUpload: fileUpload,
                    correlationId: correlationId,
                    workspace: workspace,
                    transaction: transaction),

                _ => throw new ArgumentOutOfRangeException(
                    paramName: nameof(fileUpload.UploadAlgorithm),
                    message: $"Unknown File Upload algorithm value: '{fileUpload.UploadAlgorithm}'")
            };

            queue.EnqueueWorkspaceSizeUpdateJob(
                clock: clock,
                workspaceId: workspace.Id,
                correlationId: correlationId,
                dbWriteContext: dbWriteContext,
                transaction: transaction);

            transaction.Commit();

            Logger.Debug(
                "Successfully completed FileUpload#{FileUploadId} conversion.",
                fileUpload.Id);

            return result;

        }
        catch (Exception ex)
        {
            transaction.Rollback();

            Logger.Error(ex,
                "Error converting FileUpload#{FileUploadId}. Rolling back transaction.",
                fileUpload.Id);

            throw;
        }
    }

    private Result HandleConversionForDirectUploads(
        DbWriteQueue.Context dbWriteContext,
        FileUpload fileUpload,
        SqliteTransaction transaction)
    {
        var fileId = dbWriteContext
            .OneRowCmd(
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
                        $isUploadCompleted,
                        fu_owner_identity_type,
                        fu_owner_identity,
                        $createdAt,
                        fu_encryption_key_version,
                        fu_encryption_salt,
                        fu_encryption_nonce_prefix,
                        fu_parent_file_id,
                        fu_file_metadata
                    FROM fu_file_uploads
                    WHERE fu_id = $fileUploadId
                    RETURNING fi_id",
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .WithParameter("$fileUploadId", fileUpload.Id)
            .WithParameter("$isUploadCompleted", true)
            .WithParameter("$createdAt", clock.UtcNow)
            .Execute();

        if (fileId.IsEmpty)
        {
            throw new InvalidOperationException(
                $"Failed to insert File from FileUpload#{fileUpload.Id}");
        }

        Logger.Debug("Successfully inserted file for FileUpload#{FileUploadId}",
            fileUpload.Id);

        var deletedId = dbWriteContext
            .OneRowCmd(
                sql: @"
                    DELETE FROM fu_file_uploads
                    WHERE fu_id = $fileUploadId
                    RETURNING fu_id",
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .WithParameter("$fileUploadId", fileUpload.Id)
            .Execute();

        if (deletedId.IsEmpty)
        {
            Logger.Warning(
                "Failed to delete FileUpload#{FileUploadId}. FileUpload not found.",
                fileUpload.Id);
        }
        else
        {
            Logger.Debug(
                "Successfully deleted FileUpload#{FileUploadId}.",
                fileUpload.Id);
        }

        return new Result(
            Code: ResultCode.Ok,
            Details: new Details(
                FileId: fileId.Value));
    }

    private Result HandleConversionForSingleChunkUploads(
        DbWriteQueue.Context dbWriteContext,
        FileUpload fileUpload,
        SqliteTransaction transaction)
    {
        //for now, logic is exactly the same
        return HandleConversionForDirectUploads(dbWriteContext, fileUpload, transaction);
    }

    private Result HandleConversionForMultiStepChunkUploads(
        DbWriteQueue.Context dbWriteContext,
        FileUpload fileUpload,
        WorkspaceContext workspace,
        Guid correlationId,
        SqliteTransaction transaction)
    {
        var allFileUploadPartsCount = FileParts.GetTotalNumberOfParts(
            fileSizeInBytes: fileUpload.FileSizeInBytes,
            storageEncryptionType: workspace.Storage.EncryptionType);

        var filePartsCount = dbWriteContext
            .OneRowCmd(
                sql: @"
                    SELECT COUNT(*)
                    FROM fup_file_upload_parts
                    WHERE fup_file_upload_id = $fileUploadId",
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .WithParameter("$fileUploadId", fileUpload.Id)
            .Execute();

        if (filePartsCount.IsEmpty || filePartsCount.Value != allFileUploadPartsCount)
        {
            Logger.Warning(
                "Could not convert FileUpload#{FileUploadId} to File " +
                "in Workspace#{WorkspaceId} because FileUpload was not yet completed. " +
                "Completed parts: {CompletedParts}/{AllParts}",
                fileUpload.Id,
                workspace.Id,
                filePartsCount.Value,
                allFileUploadPartsCount);

            return new Result(
                Code: ResultCode.FileUploadNotYetCompleted,
                Details: new());
        }

        var fileId = dbWriteContext
            .OneRowCmd(
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
                        $isUploadCompleted,
                        fu_owner_identity_type,
                        fu_owner_identity,
                        $createdAt,
                        fu_encryption_key_version,
                        fu_encryption_salt,
                        fu_encryption_nonce_prefix,
                        fu_parent_file_id,
                        fu_file_metadata
                    FROM fu_file_uploads
                    WHERE fu_id = $fileUploadId
                    RETURNING fi_id",
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .WithParameter("$fileUploadId", fileUpload.Id)
            .WithParameter("$isUploadCompleted", false)
            .WithParameter("$createdAt", clock.UtcNow)
            .Execute();

        if (fileId.IsEmpty)
        {
            throw new InvalidOperationException(
                $"Failed to insert File from FileUpload#{fileUpload.Id}");
        }

        Logger.Debug("Successfully inserted file for FileUpload#{FileUploadId}",
            fileUpload.Id);

        var completeFileUploadJob = EnqueueCompleteFileUploadJob(
            dbWriteContext: dbWriteContext,
            fileUploadId: fileUpload.Id,
            correlationId: correlationId,
            transaction: transaction);

        Logger.Debug(
            "Successfully enqueued complete FileUpload#{FileUploadId} job, QueueJobId: {QueueJobId}",
            fileUpload.Id, completeFileUploadJob.Value);

        var completedFileUploadId = dbWriteContext
            .OneRowCmd(
                sql: @"
                    UPDATE fu_file_uploads
                    SET fu_is_completed = TRUE
                    WHERE fu_id = $fileUploadId
                    RETURNING fu_id",
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .WithParameter("$fileUploadId", fileUpload.Id)
            .Execute();

        if (completedFileUploadId.IsEmpty)
        {
            Logger.Warning(
                "Failed to mark FileUpload#{FileUploadId} as completed. FileUpload not found.",
                fileUpload.Id);
        }
        else
        {
            Logger.Debug(
                "Successfully marked FileUpload#{FileUploadId} as completed.",
                fileUpload.Id);
        }

        return new Result(
            Code: ResultCode.Ok,
            Details: new Details(
                FileId: fileId.Value));
    }

    private QueueJobId EnqueueCompleteFileUploadJob(
        DbWriteQueue.Context dbWriteContext,
        int fileUploadId,
        Guid correlationId,
        SqliteTransaction transaction)
    {
        return queue.EnqueueOrThrow(
            correlationId: correlationId,
            jobType: CompleteS3UploadQueueJobType.Value,
            definition: new CompleteFileUploadQueueJobDefinition(
                FileUploadId: fileUploadId),
            executeAfterDate: clock.UtcNow,
            debounceId: null,
            sagaId: null,
            dbWriteContext: dbWriteContext,
            transaction: transaction);
    }

    public record Result(
        ResultCode Code,
        Details Details);

    public readonly record struct Details(
        int FileId);

    public enum ResultCode
    {
        Ok = 0,
        FileUploadNotYetCompleted
    }

    public readonly record struct FileUpload(
        int Id,
        UploadAlgorithm UploadAlgorithm,
        long FileSizeInBytes);
}