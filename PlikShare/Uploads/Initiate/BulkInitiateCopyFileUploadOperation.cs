using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Queue;
using PlikShare.Core.SQLite;
using PlikShare.Core.UserIdentity;
using PlikShare.Core.Utils;
using PlikShare.Files.Id;
using PlikShare.Storages;
using PlikShare.Storages.FileCopying;
using PlikShare.Storages.FileCopying.BulkInitiateCopyFiles;
using PlikShare.Storages.FileCopying.CopyFile;
using PlikShare.Storages.S3;
using PlikShare.Uploads.Algorithm;
using PlikShare.Uploads.Id;
using PlikShare.Workspaces.Cache;

namespace PlikShare.Uploads.Initiate;

public class BulkInitiateCopyFileUploadOperation(
    PlikShareDb plikShareDb,
    DbWriteQueue dbWriteQueue,
    IQueue queue,
    IClock clock)
{
    /// <summary>
    /// This function is one of the hot paths of the system.
    /// It is crucial to make it as fast as possible so that users could seamlessly upload lots of files into their PlikShare
    /// </summary>
    public async ValueTask<Result> Execute(
        WorkspaceContext destinationWorkspace,
        BulkInitiateCopyFilesQueueJobDefinition definition,
        IUserIdentity userIdentity,
        Guid correlationId,
        CancellationToken cancellationToken = default)
    {
        var filesToCopy = GetFilesToCopy(
            fileIds: definition.Files.Select(file => file.Id).ToArray(),
            storageClient: destinationWorkspace.Storage);

        if (destinationWorkspace.Storage is S3StorageClient s3StorageClient)
        {
            await InitiateS3MultiPartFileUploadWhereNeeded(
                s3StorageClient: s3StorageClient,
                bucketName: destinationWorkspace.BucketName,
                filesToCopy: filesToCopy, 
                cancellationToken: cancellationToken);
        }

        await InsertFileUploadsAndUpdateCopyFileQueueJobs(
            definition: definition,
            filesToCopy: filesToCopy, 
            correlationId: correlationId,
            cancellationToken: cancellationToken);
        
        return new Result(Code: ResultCode.Ok);
    }

    private async Task InitiateS3MultiPartFileUploadWhereNeeded(
        S3StorageClient s3StorageClient,
        string bucketName,
        List<FileToCopy> filesToCopy,
        CancellationToken cancellationToken)
    {
        const int batchSize = 10;

        var forAsyncProcessing = filesToCopy
            .Where(ftc => ftc.UploadAlgorithm == UploadAlgorithm.MultiStepChunkUpload);

        foreach (var batch in forAsyncProcessing.Chunk(batchSize))
        {
            var tasks = batch.Select(async fileToCopy =>
            {
                var initiatedUpload = await s3StorageClient.InitiateMultiPartUpload(
                    bucketName: bucketName,
                    key: new S3FileKey
                    {
                        FileExternalId = fileToCopy.NewFileExternalId,
                        S3KeySecretPart = fileToCopy.NewFileS3KeySecretPart
                    },
                    cancellationToken: cancellationToken);

                fileToCopy.S3UploadId = initiatedUpload.S3UploadId;
            });

            await Task.WhenAll(tasks);
        }
    }
    
    private List<FileToCopy> GetFilesToCopy(
        int[] fileIds,
        IStorageClient storageClient)
    {
        using var connection = plikShareDb.OpenConnection();

        return connection
            .Cmd(
                sql: @"
                    SELECT
                        fi_id,
                        fi_size_in_bytes,
                        fi_name,
                        fi_content_type,
                        fi_extension
                    FROM fi_files
                    WHERE fi_id IN (
                        SELECT value FROM json_each($fileIds)
                    )
                ",
                readRowFunc: reader =>
                {
                    var fileId = reader.GetInt32(0);
                    var fileSizeInBytes = reader.GetInt64(1);

                    var (algorithm, filePartsCount) = storageClient.ResolveCopyUploadAlgorithm(
                        fileSizeInBytes: fileSizeInBytes);

                    var encryptionDetails = storageClient.GenerateFileEncryptionDetails();

                    return new FileToCopy
                    {
                        Id = fileId,
                        SizeInBytes = fileSizeInBytes,
                        Name = reader.GetString(2),
                        ContentType = reader.GetString(3),
                        Extension = reader.GetString(4),

                        FileUploadExternalId = FileUploadExtId.NewId(),
                        UploadAlgorithm = algorithm,
                        FilePartsCount = filePartsCount,
                        S3UploadId = string.Empty,

                        NewFileExternalId = FileExtId.NewId(),
                        NewFileS3KeySecretPart = storageClient.GenerateFileS3KeySecretPart(),

                        NewFileEncryptionKeyVersion = encryptionDetails.Metadata?.KeyVersion,
                        NewFileEncryptionSalt = encryptionDetails.Metadata?.Salt,
                        NewFileEncryptionNoncePrefix = encryptionDetails.Metadata?.NoncePrefix
                    };
                })
            .WithJsonParameter("$fileIds", fileIds)
            .Execute();
    }

    private Task<Dictionary<FileUploadExtId, FileUpload>> InsertFileUploadsAndUpdateCopyFileQueueJobs(
        BulkInitiateCopyFilesQueueJobDefinition definition,
        List<FileToCopy> filesToCopy,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => InsertFileUploadsAndUpdateCopyFileQueueJobs(
                dbWriteContext: context,
                definition: definition,
                filesToCopy: filesToCopy,
                correlationId: correlationId),
            cancellationToken: cancellationToken);
    }

    private Dictionary<FileUploadExtId, FileUpload> InsertFileUploadsAndUpdateCopyFileQueueJobs(
        DbWriteQueue.Context dbWriteContext,
        BulkInitiateCopyFilesQueueJobDefinition definition,
        List<FileToCopy> filesToCopy,
        Guid correlationId)
    {
        dbWriteContext.Connection.RegisterJsonArrayToBlobFunction();
        using var transaction = dbWriteContext.Connection.BeginTransaction();

        try
        {
            var fileUploadsDict = dbWriteContext
                .Cmd(
                    sql: @"
                        INSERT INTO fu_file_uploads(
                            fu_external_id,
                            fu_workspace_id,
                            fu_folder_id,
                            fu_s3_upload_id,
                            fu_owner_identity_type,
                            fu_owner_identity,
                            fu_file_name,
                            fu_file_extension,
                            fu_file_content_type,
                            fu_file_size_in_bytes,
                            fu_file_external_id,
                            fu_file_s3_key_secret_part,
                            fu_encryption_key_version,
                            fu_encryption_salt,
                            fu_encryption_nonce_prefix,
                            fu_is_completed,
                            fu_parent_file_id,
                            fu_file_metadata
                        )
                        SELECT
                            json_extract(value, '$.fileUploadExternalId'),
                            $workspaceId,
                            NULL,
                            json_extract(value, '$.s3UploadId'),
                            $ownerIdentityType,
                            $ownerIdentity,
                            json_extract(value, '$.name'),
                            json_extract(value, '$.extension'),
                            json_extract(value, '$.contentType'),
                            json_extract(value, '$.sizeInBytes'),
                            json_extract(value, '$.newFileExternalId'),
                            json_extract(value, '$.newFileS3KeySecretPart'),
                            json_extract(value, '$.newFileEncryptionKeyVersion'),
                            app_json_array_to_blob(json_extract(value, '$.newFileEncryptionSalt')),
                            app_json_array_to_blob(json_extract(value, '$.newFileEncryptionNoncePrefix')),
                            FALSE,
                            NULL,
                            NULL
                        FROM
                            json_each($fileUploads)
                        RETURNING 
                            fu_id,
                            fu_external_id
                    ",
                    readRowFunc: reader => new FileUpload
                    {
                        Id = reader.GetInt32(0),
                        ExternalId = reader.GetExtId<FileUploadExtId>(1)
                    },
                    transaction: transaction)
                .WithParameter("$workspaceId", definition.DestinationWorkspaceId)
                .WithParameter("$ownerIdentityType", definition.UserIdentityType)
                .WithParameter("$ownerIdentity", definition.UserIdentity)
                .WithJsonParameter("$fileUploads", filesToCopy)
                .Execute()
                .ToDictionary(
                    keySelector: fu => fu.ExternalId,
                    elementSelector: fu => fu);

            var copyFileQueueJobsFileUploads = new List<CopyFileQueueJobFileUpload>();

            for (var i = 0; i < filesToCopy.Count; i++)
            {
                var fileToCopy = filesToCopy[i];
                var fileUpload = fileUploadsDict[fileToCopy.FileUploadExternalId];
                var onCompleted = definition
                    .Files
                    .First(x => x.Id == fileToCopy.Id)
                    .OnCompleted;

                copyFileQueueJobsFileUploads.Add(new CopyFileQueueJobFileUpload
                {
                    FileId = fileToCopy.Id,
                    OnCompleted = Json.Serialize(onCompleted),
                    FileUploadId = fileUpload.Id,
                    UploadAlgorithm = fileToCopy.UploadAlgorithm.ToKebabCase()
                });
            }

            var initiatedJobIds = dbWriteContext
                .Cmd(
                    sql: @"
                        INSERT INTO cfq_copy_file_queue(
                            cfq_file_id,
                            cfq_source_workspace_id,
                            cfq_file_upload_id,
                            cfq_destination_workspace_id,
                            cfq_upload_algorithm,
                            cfq_status,
                            cfq_on_completed_action,
                            cfq_correlation_id
                        )
                        SELECT
                            json_extract(value, '$.fileId'),
                            $sourceWorkspaceId,
                            json_extract(value, '$.fileUploadId'),
                            $destinationWorkspaceId,
                            json_extract(value, '$.uploadAlgorithm'),
                            $pendingStatus,
                            json_extract(value, '$.onCompleted'),
                            $correlationId
                        FROM
                            json_each($copyFileQueueJobsFileUploads)
                        RETURNING 
                            cfq_id
                        
                    ",
                    readRowFunc: reader => reader.GetInt32(0),
                    transaction: transaction
                )
                .WithParameter("$sourceWorkspaceId", definition.SourceWorkspaceId)
                .WithParameter("$destinationWorkspaceId", definition.DestinationWorkspaceId)
                .WithJsonParameter("$copyFileQueueJobsFileUploads", copyFileQueueJobsFileUploads)
                .WithEnumParameter("$pendingStatus", CopyFileQueueStatus.Pending)
                .WithParameter("$correlationId", correlationId)
                .Execute();

            var queueJobs = initiatedJobIds
                .Select(jobId => queue.CreateBulkEntity(
                    jobType: CopyFileQueueJobType.Value,
                    definition: new CopyFileQueueJobDefinition
                    {
                        CopyFileQueueJobId = jobId
                    },
                    sagaId: null))
                .ToList();

            queue.EnqueueBulk(
                correlationId: correlationId,
                definitions: queueJobs,
                executeAfterDate: clock.UtcNow,
                dbWriteContext,
                transaction);

            transaction.Commit();

            return fileUploadsDict;
        }
        catch (Exception e)
        {
            transaction.Rollback();

            Console.WriteLine(e);
            throw;
        }
    }

    public class FileToCopy
    {
        public required int Id { get; init; }
        public required string Name { get; init; }
        public required long SizeInBytes { get; init; }
        public required string ContentType { get; init; }
        public required string Extension { get; init; }


        public required FileUploadExtId FileUploadExternalId { get; init; }
        public required UploadAlgorithm UploadAlgorithm { get; init; }
        public required int FilePartsCount { get; init; }

        //that one is mutable not to allocate memory when it can be avoided
        public required string S3UploadId { get; set; }

        public required FileExtId NewFileExternalId { get; init; }
        public required string NewFileS3KeySecretPart { get; init; }

        public byte? NewFileEncryptionKeyVersion { get; init; }
        public byte[]? NewFileEncryptionSalt { get; init; }
        public byte[]? NewFileEncryptionNoncePrefix { get; init; }
    }

    private class FileUpload
    {
        public required int Id { get; init; }
        public required FileUploadExtId ExternalId { get; init; }
    }

    public class CopyFileQueueJobFileUpload
    {
        public required int FileId { get; init; }
        public required string OnCompleted { get; init; }
        public required int FileUploadId { get; init; }
        public required string UploadAlgorithm { get; init; }
    }

    public record Result(
        ResultCode Code);

    public enum ResultCode
    {
        Ok = 0,
    }
}