using Microsoft.Data.Sqlite;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Queue;
using PlikShare.Core.SQLite;
using PlikShare.Files.BulkDelete.QueueJob;
using PlikShare.Files.Id;
using PlikShare.Storages;

namespace PlikShare.Files.Delete
{
    public class DeleteFilesSubQuery(IQueue queue)
    {
        public Result Execute(
            int workspaceId,
            List<int> fileIds,
            QueueSagaId? sagaId,
            DbWriteQueue.Context dbWriteContext,
            SqliteTransaction transaction)
        {
            var workspace = GetWorkspace(
                workspaceId,
                dbWriteContext,
                transaction);

            var deletedFiles = DeleteFileEntities(
                workspaceId,
                fileIds,
                dbWriteContext,
                transaction);
            
            var jobsToEnqueue = new List<BulkQueueJobEntity>();

            foreach (var deletedFilesChunk in deletedFiles.Chunk(BulkDeleteS3FileQueueJobType.MaxChunkSize))
            {
                var job = queue.CreateBulkEntity(
                    jobType: BulkDeleteS3FileQueueJobType.Value,
                    definition: new BulkDeleteS3FileQueueJobDefinition
                    {
                        BucketName = workspace.BucketName,
                        StorageId = workspace.StorageId,
                        S3FileKeys = deletedFilesChunk
                            .Select(df => new S3FileKey
                            {
                                FileExternalId = df.ExternalId,
                                S3KeySecretPart = df.S3KeySecretPart
                            })
                            .ToArray()
                    },
                    sagaId: sagaId);

                jobsToEnqueue.Add(job);
            }

            return new Result(
                Files: deletedFiles,
                JobsToEnqueue: jobsToEnqueue);
        }

        private static List<DeletedFile> DeleteFileEntities(
            int workspaceId,
            List<int> fileIds,
            DbWriteQueue.Context dbWriteContext,
            SqliteTransaction transaction)
        {
            if (fileIds.Count == 0)
                return [];

            var deletedFiles = dbWriteContext
                .Cmd(
                    sql: @"
                        DELETE FROM fi_files
                        WHERE
                            fi_workspace_id = $workspaceId
                            AND fi_id IN (
                                SELECT value FROM json_each($fileIds)
                            )      
                        RETURNING 
                            fi_id,
                            fi_external_id,
                            fi_s3_key_secret_part
                    ",
                    readRowFunc: reader => new DeletedFile
                    {
                        Id = reader.GetInt32(0),
                        ExternalId = reader.GetExtId<FileExtId>(1),
                        S3KeySecretPart = reader.GetString(2)
                    },
                    transaction: transaction)
                .WithParameter("$workspaceId", workspaceId)
                .WithJsonParameter("$fileIds", fileIds)
                .Execute();

            var deletedDependentFiles = dbWriteContext
                .Cmd(
                    sql: @"
                    DELETE FROM fi_files
                    WHERE 
                        fi_workspace_id = $workspaceId
                        AND fi_parent_file_id IN (
                            SELECT value FROM json_each($deletedFileIds)
                        )
                    RETURNING 
                        fi_id,
                        fi_external_id,
                        fi_s3_key_secret_part
                ",
                    readRowFunc: reader => new DeletedFile
                    {
                        Id = reader.GetInt32(0),
                        ExternalId = reader.GetExtId<FileExtId>(1),
                        S3KeySecretPart = reader.GetString(2)
                    },
                    transaction: transaction)
                .WithParameter("$workspaceId", workspaceId)
                .WithJsonParameter("$deletedFileIds", deletedFiles.Select(df => df.Id).ToArray())
                .Execute();

            deletedFiles.AddRange(deletedDependentFiles);

            return deletedFiles;
        }

        private static Workspace GetWorkspace(
            int workspaceId,
            DbWriteQueue.Context dbWriteContext,
            SqliteTransaction transaction)
        {
            return dbWriteContext
                .OneRowCmd(
                    sql: @"
                        SELECT 
                            w_storage_id,
                            w_bucket_name
                        FROM w_workspaces
                        WHERE w_id = $workspaceId
                    ",
                    readRowFunc: reader => new Workspace(
                        StorageId: reader.GetInt32(0),
                        BucketName: reader.GetString(1)),
                    transaction: transaction)
                .WithJsonParameter("$workspaceId", workspaceId)
                .ExecuteOrThrow();
        }

        private readonly record struct Workspace(
            int StorageId,
            string BucketName);

        public class DeletedFile
        {
            public required int Id { get; init; }
            public required FileExtId ExternalId { get; init; }
            public required string S3KeySecretPart { get; init; }
        }
        
        public readonly record struct Result(
            List<DeletedFile> Files,
            List<BulkQueueJobEntity> JobsToEnqueue);
    }
}
