using Microsoft.Data.Sqlite;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Queue;
using PlikShare.Core.SQLite;
using PlikShare.Uploads.Delete;

namespace PlikShare.Storages.FileCopying.Delete
{
    public class DeleteCopyFileQueueJobsSubQuery(DeleteFileUploadsSubQuery deleteFileUploadsSubQuery)
    {
        public Result Execute(
            int workspaceId,
            int[] deletedFileIds,
            int[] deletedFileUploadIds,
            DbWriteQueue.Context dbWriteContext,
            SqliteTransaction transaction)
        {
            var deletedSourceCopyFileQueueJobs = DeleteSourceCopyFileQueueJobs(
                sourceWorkspaceId: workspaceId,
                deletedFileIds: deletedFileIds,
                dbWriteContext: dbWriteContext,
                transaction: transaction);

            var (deletedCopyFileUploads, deletedCopyFileUploadParts, copyFileUploadJobs) = deleteFileUploadsSubQuery.Execute(
                fileUploadIds: deletedSourceCopyFileQueueJobs.Select(x => x.FileUploadId).ToList(),
                sagaId: null,
                dbWriteContext: dbWriteContext,
                transaction: transaction);
            
            var deletedDestinationCopyFileQueueJobs = DeleteDestinationCopyFileQueueJobs(
                destinationWorkspaceId: workspaceId,
                deletedFileUploadIds: deletedFileUploadIds,
                dbWriteContext: dbWriteContext,
                transaction: transaction);

            return new Result(
                CopyFileQueueJobs: [..deletedSourceCopyFileQueueJobs, ..deletedDestinationCopyFileQueueJobs],
                FileUploads: deletedCopyFileUploads,
                FileUploadParts: deletedCopyFileUploadParts,
                JobsToEnqueue: copyFileUploadJobs);
        }

        //for all this copy queue jobs we also need to delete file uploads
        //because they will pend indefinitely otherwise
        private static List<DeletedCopyFileQueueJob> DeleteSourceCopyFileQueueJobs(
            int sourceWorkspaceId,
            int[] deletedFileIds,
            DbWriteQueue.Context dbWriteContext,
            SqliteTransaction transaction)
        {
            if (deletedFileIds.Length == 0)
                return [];

            return dbWriteContext
                .Cmd(
                    sql: @"
                    DELETE FROM cfq_copy_file_queue
                    WHERE
                        cfq_source_workspace_id = $sourceWorkspaceId
                        AND cfq_file_id IN (
                            SELECT value FROM json_each($deletedFileIds)
                        )
                    RETURNING 
                        cfq_id,
                        cfq_file_upload_id,
                        cfq_source_workspace_id,
                        cfq_destination_workspace_id
                ",
                    readRowFunc: reader => new DeletedCopyFileQueueJob(
                        Id: reader.GetInt32(0),
                        FileUploadId: reader.GetInt32(1),
                        SourceWorkspaceId: reader.GetInt32(2),
                        DestinationWorkspaceId: reader.GetInt32(3)),
                    transaction: transaction)
                .WithParameter("$sourceWorkspaceId", sourceWorkspaceId)
                .WithJsonParameter("$deletedFileIds", deletedFileIds)
                .Execute();
        }

        //for this copy file queue jobs we dont need to do anything more - just delete the entities
        private static List<DeletedCopyFileQueueJob> DeleteDestinationCopyFileQueueJobs(
            int destinationWorkspaceId,
            int[] deletedFileUploadIds,
            DbWriteQueue.Context dbWriteContext,
            SqliteTransaction transaction)
        {
            if (deletedFileUploadIds.Length == 0)
                return [];

            return dbWriteContext
                .Cmd(
                    sql: @"
                    DELETE FROM cfq_copy_file_queue
                    WHERE
                        cfq_destination_workspace_id = $destinationWorkspaceId
                        AND cfq_file_upload_id IN (
                            SELECT value FROM json_each($deletedFileUploadIds)
                        )
                    RETURNING 
                        cfq_id,
                        cfq_file_upload_id,
                        cfq_source_workspace_id,
                        cfq_destination_workspace_id
                ",
                    readRowFunc: reader => new DeletedCopyFileQueueJob(
                        Id: reader.GetInt32(0),
                        FileUploadId: reader.GetInt32(1),
                        SourceWorkspaceId: reader.GetInt32(2),
                        DestinationWorkspaceId: reader.GetInt32(3)),
                    transaction: transaction)
                .WithParameter("$destinationWorkspaceId", destinationWorkspaceId)
                .WithJsonParameter("$deletedFileUploadIds", deletedFileUploadIds)
                .Execute();
        }

        public readonly record struct DeletedCopyFileQueueJob(
            int Id,
            int FileUploadId,
            int SourceWorkspaceId,
            int DestinationWorkspaceId);

        public readonly record struct Result(
            List<DeletedCopyFileQueueJob> CopyFileQueueJobs,
            List<DeleteFileUploadsSubQuery.DeletedFileUpload> FileUploads,
            List<DeleteFileUploadsSubQuery.DeletedFileUploadPart> FileUploadParts,
            List<BulkQueueJobEntity> JobsToEnqueue);
    }
}
