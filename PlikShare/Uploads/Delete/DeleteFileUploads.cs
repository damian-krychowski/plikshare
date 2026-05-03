using Microsoft.Data.Sqlite;
using PlikShare.Core.Encryption;
using PlikShare.Core.Queue;
using PlikShare.Core.SQLite;
using PlikShare.Files.Id;
using PlikShare.Storages;
using PlikShare.Uploads.Abort.QueueJob;

namespace PlikShare.Uploads.Delete;

public class DeleteFileUploadsSubQuery(
    IQueue queue,
    StorageClientStore storageClientStore)
{
    public Result Execute(
        List<int> fileUploadIds,
        QueueSagaId? sagaId,
        SqliteWriteContext dbWriteContext,
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

            var partTokens = deletedFileUploadParts
                .Where(p => p.FileUploadId == deletedFileUpload.Id)
                .Select(p => p.ETag)
                .ToList();

            if (!storageClientStore.TryGetClient(workspace.StorageId, out var storage))
                throw new InvalidOperationException(
                    $"Cannot enqueue multipart-upload abort for File '{deletedFileUpload.FileExternalId}': " +
                    $"Storage#{workspace.StorageId} is not registered in StorageClientStore.");

            var abortHandle = storage.BuildAbortHandle(
                uploadId: deletedFileUpload.MultipartUploadId,
                partTokens: partTokens);

            var job = queue.CreateBulkEntity(
                jobType: AbortMultipartUploadQueueJobType.Value,
                definition: new AbortMultipartUploadQueueJobDefinition
                {
                    StorageId = workspace.StorageId,
                    BucketName = workspace.BucketName,
                    FileExternalId = deletedFileUpload.FileExternalId,
                    KeySecretPart = deletedFileUpload.FileKeySecretPart,
                    AbortHandle = abortHandle
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
        SqliteWriteContext dbWriteContext,
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
        SqliteWriteContext dbWriteContext,
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
                        fu_file_key_secret_part,
                        fu_multipart_upload_id,
                        fu_file_size_in_bytes,
                        fu_workspace_id,
                        fu_encryption_key_version,
                        fu_encryption_salt,
                        fu_encryption_nonce_prefix,
                        fu_encryption_chain_salts,
                        fu_encryption_format_version
                ",
                readRowFunc: reader =>
                {
                    var encryptionKeyVersion = reader.GetByteOrNull(6);

                    return new DeletedFileUpload
                    {
                        Id = reader.GetInt32(0),
                        FileExternalId = reader.GetExtId<FileExtId>(1),
                        FileKeySecretPart = reader.GetString(2),
                        MultipartUploadId = reader.GetString(3),
                        FileSizeInBytes = reader.GetInt64(4),
                        WorkspaceId = reader.GetInt32(5),
                        FileEncryptionMetadata = encryptionKeyVersion is null
                            ? null
                            : new FileEncryptionMetadata
                            {
                                KeyVersion = encryptionKeyVersion.Value,
                                Salt = reader.GetFieldValue<byte[]>(7),
                                NoncePrefix = reader.GetFieldValue<byte[]>(8),
                                ChainStepSalts = KeyDerivationChain.Deserialize(
                                    reader.GetFieldValueOrNull<byte[]>(9)),
                                FormatVersion = reader.GetByteOrNull(10) ?? 1
                            }
                    };
                },
                transaction: transaction)
            .WithJsonParameter("$fileUploadIds", fileUploadIds)
            .Execute();
    }

    private static List<Workspace> GetWorkspaces(
        int[] workspaceIds,
        SqliteWriteContext dbWriteContext,
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
        public required string FileKeySecretPart { get; init; }
        public required string MultipartUploadId { get; init; }
        public required long FileSizeInBytes { get; init; }
        public required int WorkspaceId { get; init; }
        public required FileEncryptionMetadata? FileEncryptionMetadata { get; init; }
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