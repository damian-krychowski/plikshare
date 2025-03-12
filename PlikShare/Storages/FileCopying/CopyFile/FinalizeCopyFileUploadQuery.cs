using Microsoft.Data.Sqlite;
using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Queue;
using PlikShare.Core.SQLite;
using PlikShare.Storages.FileCopying.OnCompletedActionHandler;
using PlikShare.Workspaces.UpdateCurrentSizeInBytes.QueueJob;
using Serilog;

namespace PlikShare.Storages.FileCopying.CopyFile;

public class FinalizeCopyFileUploadQuery(
    IQueue queue,
    IClock clock,
    DbWriteQueue dbWriteQueue,
    IEnumerable<ICopyFileQueueCompletedActionHandler> onCompletedHandlers)
{
    private static readonly Serilog.ILogger Logger = Log.ForContext<FinalizeCopyFileUploadQuery>();

    public Task<ResultCode> Execute(
        int copyFileQueueJobId,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                copyFileQueueJobId: copyFileQueueJobId),
            cancellationToken: cancellationToken);
    }

    private ResultCode ExecuteOperation(
        DbWriteQueue.Context dbWriteContext,
        int copyFileQueueJobId)
    {
        using var transaction = dbWriteContext.Connection.BeginTransaction();

        try
        {
            var result = dbWriteContext
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
                        INNER JOIN cfq_copy_file_queue
                            ON cfq_file_upload_id = fu_id
                        WHERE 
                            cfq_id = $cfqId
                        RETURNING 
                            fi_id,
                            fi_workspace_id",
                    readRowFunc: reader => new
                    {
                        FileId = reader.GetInt32(0),
                        WorkspaceId = reader.GetInt32(1)
                    },
                    transaction: transaction)
                .WithParameter("$cfqId", copyFileQueueJobId)
                .WithParameter("$createdAt", clock.UtcNow)
                .ExecuteOrThrow();

            var deletedCfq = dbWriteContext
                .OneRowCmd(
                    sql: @"
                        DELETE FROM cfq_copy_file_queue
                        WHERE cfq_id = $cfqId
                        RETURNING 
                            cfq_file_id,
                            cfq_source_workspace_id,
                            cfq_on_completed_action,
                            cfq_correlation_id,
                            cfq_file_upload_id",
                    readRowFunc: reader => new
                    {
                        SourceFileId = reader.GetInt32(0),
                        SourceWorkspaceId = reader.GetInt32(1),
                        OnCompletedAction = reader.GetFromJson<CopyFileQueueOnCompletedActionDefinition>(2),
                        CorrelationId = reader.GetGuid(3),
                        FileUploadId = reader.GetInt32(4)
                    },
                    transaction: transaction)
                .WithParameter("$cfqId", copyFileQueueJobId)
                .ExecuteOrThrow();

            var deleteFileUploadParts = dbWriteContext
                .Cmd(
                    sql: @"
                        DELETE FROM fup_file_upload_parts
                        WHERE fup_file_upload_id = $fileUploadId
                        RETURNING fup_part_number
                    ",
                    readRowFunc: reader => reader.GetInt32(0),
                    transaction: transaction)
                .WithParameter("$fileUploadId", deletedCfq.FileUploadId)
                .Execute();

            var deletedFileUploadId = dbWriteContext
                .OneRowCmd(
                    sql: @"
                        DELETE FROM fu_file_uploads
                        WHERE fu_id = $fileUploadId
                        RETURNING fu_id",
                    readRowFunc: reader => reader.GetInt32(0),
                    transaction: transaction)
                .WithParameter("$fileUploadId", deletedCfq.FileUploadId)
                .Execute();

            if (deletedFileUploadId.IsEmpty)
            {
                //todo improve that log, tell expilicely which file uploads where not found
                Logger.Warning(
                    "Failed to delete file upload. FileUploads  where not found");
            }
            else
            {
                Logger.Debug(
                    "Successfully deleted file upload. FileUploadId: {FileUploadId}",
                    deletedFileUploadId);
            }

            EnqueueWorkspaceSizeUpdateJob(
                dbWriteContext: dbWriteContext,
                workspaceId: result.WorkspaceId,
                correlationId: deletedCfq.CorrelationId,
                transaction: transaction);

            var onCompletedHandler = onCompletedHandlers
                .FirstOrDefault(x => x.HandlerType == deletedCfq.OnCompletedAction.HandlerType);

            if (onCompletedHandler is null)
            {
                //todo: handle gracefully
                throw new NotImplementedException();
            }

            onCompletedHandler.OnCopyFileCompleted(
                dbWriteContext: dbWriteContext,
                actionHandlerDefinition: deletedCfq.OnCompletedAction.ActionHandlerDefinition!,
                sourceFileId: deletedCfq.SourceFileId,
                sourceWorkspaceId: deletedCfq.SourceWorkspaceId,
                newFileId: result.FileId,
                destinationWorkspaceId: result.WorkspaceId,
                correlationId: deletedCfq.CorrelationId,
                transaction: transaction);

            transaction.Commit();

            Logger.Debug(
                "Successfully completed CopyFileQueueJob#{CopyFileQueueJobId}.", copyFileQueueJobId);

            return ResultCode.Ok;

        }
        catch (Exception ex)
        {
            transaction.Rollback();

            Logger.Error(ex,
                "Error in finalizing CopyFileQueueJob#{CopyFileQueueJobId}. Rolling back transaction.",
                copyFileQueueJobId);

            throw;
        }
    }

    private QueueJobId EnqueueWorkspaceSizeUpdateJob(
        DbWriteQueue.Context dbWriteContext,
        int workspaceId,
        Guid correlationId,
        SqliteTransaction transaction)
    {
        return queue.EnqueueOrThrow(
            correlationId: correlationId,
            jobType: UpdateWorkspaceCurrentSizeInBytesQueueJobType.Value,
            definition: new UpdateWorkspaceCurrentSizeInBytesQueueJobDefinition(
                WorkspaceId: workspaceId),
            executeAfterDate: clock.UtcNow.AddSeconds(10),
            debounceId: $"update_workspace_current_size_in_bytes_{workspaceId}",
            sagaId: null,
            dbWriteContext: dbWriteContext,
            transaction: transaction);
    }

    public enum ResultCode
    {
        Ok = 0,
    }
}