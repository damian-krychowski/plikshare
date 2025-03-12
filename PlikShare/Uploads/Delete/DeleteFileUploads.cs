using Microsoft.Data.Sqlite;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Queue;
using PlikShare.Core.SQLite;
using PlikShare.Files.Id;
using PlikShare.Uploads.Abort.QueueJob;

namespace PlikShare.Uploads.Delete;

public class DeleteFileUploadsSubQuery(IQueue queue)
{
    public Result Execute(
        List<int> fileUploadIds,
        QueueSagaId? sagaId,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction)
    {
        var deletedFileUploadParts = DeleteFileUploadPartEntities(
            fileUploadIds,
            dbWriteContext,
            transaction);

        var deletedFileUploads = DeleteFileUploadEntities(
            fileUploadIds,
            dbWriteContext,
            transaction);

        if (deletedFileUploads.Count == 0)
            return new Result(
                FileUploads: [],
                FileUploadParts: [],
                JobsToEnqueue: []);

        var workspaces = GetWorkspaces(
            workspaceIds: deletedFileUploads
                .Select(dfu => dfu.WorkspaceId)
                .Distinct()
                .ToArray(),
            dbWriteContext: dbWriteContext,
            transaction: transaction);

        var jobsToEnqueue = new List<BulkQueueJobEntity>();

        foreach (var deletedFileUpload in deletedFileUploads)
        {
            var workspace = workspaces
                .First(w => w.Id == deletedFileUpload.WorkspaceId);

            var partETags = deletedFileUploadParts
                .Where(p => p.FileUploadId == deletedFileUpload.Id)
                .Select(p => p.ETag)
                .ToList();

            var job = queue.CreateBulkEntity(
                jobType: AbortS3UploadQueueJobType.Value,
                definition: new AbortS3UploadQueueJobDefinition
                {
                    FileExternalId = deletedFileUpload.FileExternalId,
                    FileSizeInBytes = deletedFileUpload.FileSizeInBytes,
                    S3KeySecretPart = deletedFileUpload.FileS3KeySecretPart,
                    S3UploadId = deletedFileUpload.S3UploadId,

                    BucketName = workspace.BucketName,
                    StorageId = workspace.StorageId,
                    PartETags = partETags
                },
                sagaId: sagaId);

            jobsToEnqueue.Add(job);
        }

        return new Result(
            FileUploads: deletedFileUploads,
            FileUploadParts: deletedFileUploadParts,
            JobsToEnqueue: jobsToEnqueue);
    }

    private static List<DeletedFileUploadPart> DeleteFileUploadPartEntities(
        List<int> fileUploadIds,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction)
    {
        if (fileUploadIds.Count == 0)
            return [];

        return dbWriteContext
            .Cmd(
                sql: @"
                    DELETE FROM fup_file_upload_parts
                    WHERE
                        fup_file_upload_id IN (
                            SELECT value FROM json_each($fileUploadIds)
                        )
                    RETURNING 
                        fup_file_upload_id,
                        fup_part_number,
                        fup_etag
                ",
                readRowFunc: reader => new DeletedFileUploadPart
                {
                    FileUploadId = reader.GetInt32(0),
                    PartNumber = reader.GetInt32(1),
                    ETag = reader.GetString(2)
                },
                transaction: transaction)
            .WithJsonParameter("$fileUploadIds", fileUploadIds)
            .Execute();
    }

    private static List<DeletedFileUpload> DeleteFileUploadEntities(
        List<int> fileUploadIds,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction)
    {
        if (fileUploadIds.Count == 0)
            return [];

        return dbWriteContext
            .Cmd(
                sql: @"  
                    DELETE FROM fu_file_uploads
                    WHERE                  
                        fu_id IN (
                            SELECT value FROM json_each($fileUploadIds)
                        )
                    RETURNING 
                        fu_id,
                        fu_file_external_id,
                        fu_file_s3_key_secret_part,
                        fu_s3_upload_id,
                        fu_file_size_in_bytes,
                        fu_workspace_id
                ",
                readRowFunc: reader => new DeletedFileUpload{
                    Id = reader.GetInt32(0),
                    FileExternalId = reader.GetExtId<FileExtId>(1),
                    FileS3KeySecretPart = reader.GetString(2),
                    S3UploadId = reader.GetString(3),
                    FileSizeInBytes = reader.GetInt64(4),
                    WorkspaceId = reader.GetInt32(5)
                },
                transaction: transaction)
            .WithJsonParameter("$fileUploadIds", fileUploadIds)
            .Execute();
    }

    private static List<Workspace> GetWorkspaces(
        int[] workspaceIds,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction)
    {
        if (workspaceIds.Length == 0)
            return [];

        return dbWriteContext
            .Cmd(
                sql: @"
                    SELECT 
                        w_id,
                        w_storage_id,
                        w_bucket_name
                    FROM w_workspaces
                    WHERE w_id IN (
                        SELECT value FROM json_each($workspaceIds)
                    )
                ",
                readRowFunc: reader => new Workspace(
                    Id: reader.GetInt32(0),
                    StorageId: reader.GetInt32(1),
                    BucketName: reader.GetString(2)),
                transaction: transaction)
            .WithJsonParameter("$workspaceIds", workspaceIds)
            .Execute();
    }
    
    private readonly record struct Workspace(
        int Id,
        int StorageId,
        string BucketName);

    public class DeletedFileUpload
    {
        public required int Id { get; init; }
        public required FileExtId FileExternalId { get; init; }
        public required string FileS3KeySecretPart { get; init; }
        public required string S3UploadId { get; init; }
        public required long FileSizeInBytes { get; init; }
        public required int WorkspaceId { get; init; }
    }

    public class DeletedFileUploadPart
    {
        public required int FileUploadId { get; init; }
        public required int PartNumber { get; init; }
        public required string ETag { get; init; }
    }

    public record Result(
        List<DeletedFileUpload> FileUploads,
        List<DeletedFileUploadPart> FileUploadParts,
        List<BulkQueueJobEntity> JobsToEnqueue);
}