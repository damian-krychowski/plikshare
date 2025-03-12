using System.IO.Pipelines;
using Amazon.S3.Model;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.Queue;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using PlikShare.Files.Id;
using PlikShare.Files.Records;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.FileReading;
using PlikShare.Uploads.Algorithm;
using PlikShare.Uploads.Cache;
using PlikShare.Uploads.Chunking;
using PlikShare.Uploads.FilePartUpload.Complete;
using PlikShare.Workspaces.Cache;
using Serilog;

namespace PlikShare.Storages.FileCopying.CopyFile;

public class CopyFileQueueJobExecutor(
    PlikShareDb plikShareDb,
    DbWriteQueue dbWriteQueue,
    WorkspaceCache workspaceCache,
    FinalizeCopyFileUploadQuery finalizeCopyFileUploadQuery,
    InsertFileUploadPartQuery insertFileUploadPartQuery) : IQueueLongRunningJobExecutor
{
    public string JobType => CopyFileQueueJobType.Value;
    public int Priority => QueueJobPriority.Normal;

    public async Task<QueueJobResult> Execute(
        string definitionJson,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        var definition = Json.Deserialize<CopyFileQueueJobDefinition>(
            definitionJson);

        if (definition is null)
        {
            throw new ArgumentException(
                $"Job '{definitionJson}' cannot be parsed to correct '{nameof(CopyFileQueueJobDefinition)}'");
        }

        var copyFileQueueJob = TryGetCopyFileQueueJobDetails(
            copyFileQueueJobId: definition.CopyFileQueueJobId);

        if (copyFileQueueJob is null)
        {
            Log.Warning("CopyFileQueueJob#{CopyFileQueueJobId} was not found. Copying process will be skipped.",
                definition.CopyFileQueueJobId);

            return QueueJobResult.Success;
        }

        var sourceWorkspace = await workspaceCache.TryGetWorkspace(
            workspaceId: copyFileQueueJob.SourceWorkspaceId,
            cancellationToken: cancellationToken);

        if (sourceWorkspace is null)
        {
            Log.Warning("CopyFileQueueJob#{CopyFileQueueJobId} source Workspace#{WorkspaceId} was not found. Copying process will be skipped.",
                definition.CopyFileQueueJobId,
                copyFileQueueJob.SourceWorkspaceId);

            return QueueJobResult.Success;
        }

        var destinationWorkspace = await workspaceCache.TryGetWorkspace(
            workspaceId: copyFileQueueJob.TargetWorkspaceId,
            cancellationToken: cancellationToken);
        
        if (destinationWorkspace is null)
        {
            Log.Warning("CopyFileQueueJob#{CopyFileQueueJobId} destination Workspace#{WorkspaceId} was not found. Copying process will be skipped.",
                definition.CopyFileQueueJobId,
                copyFileQueueJob.TargetWorkspaceId);

            return QueueJobResult.Success;
        }

        try
        {
            var updateResult = await MarkCopyFileQueueJobAsUploading(
                copyFileQueueJobId: definition.CopyFileQueueJobId,
                cancellationToken: cancellationToken);

            if (updateResult == UpdateResult.CopyFileQueueJobNotFound)
            {
                Log.Warning("CopyFileQueueJob#{CopyFileQueueJobId} was not found and it status could not be updated. Copying process will be skipped.",
                    definition.CopyFileQueueJobId);

                return QueueJobResult.Success;
            }

            switch (copyFileQueueJob.UploadAlgorithm)
            {
                case UploadAlgorithm.MultiStepChunkUpload:
                    await CopyFileInChunks(
                        copyFileQueueJob: copyFileQueueJob,
                        sourceWorkspace: sourceWorkspace,
                        destinationWorkspace: destinationWorkspace,
                        cancellationToken: cancellationToken);
                    break;

                case UploadAlgorithm.DirectUpload:
                    await CopyFileDirectly(
                        copyFileQueueJob: copyFileQueueJob,
                        sourceWorkspace: sourceWorkspace,
                        destinationWorkspace: destinationWorkspace,
                        cancellationToken: cancellationToken);
                    break;

                default:
                    throw new NotSupportedException(
                        $"Upload algorithm '{copyFileQueueJob.UploadAlgorithm}' is not supported for copying the files");
            }

            await finalizeCopyFileUploadQuery.Execute(
                copyFileQueueJobId: copyFileQueueJob.Id,
                cancellationToken: cancellationToken);

            return QueueJobResult.Success;
        }
        catch (Exception e)
        {
            Log.Error(e, "Something went wrong while executing CopyFileQueueJob#{CopyFileQueueJobId}",
                definition.CopyFileQueueJobId);

            throw;
        }
    }

    private static async Task CopyFileDirectly(
        CopyFileQueueJob copyFileQueueJob, 
        WorkspaceContext sourceWorkspace,
        WorkspaceContext destinationWorkspace, 
        CancellationToken cancellationToken)
    {
        var pipe = new Pipe();

        var readFileTask = ReadFile(
            job: copyFileQueueJob,
            sourceWorkspace: sourceWorkspace,
            output: pipe.Writer,
            stoppingToken: cancellationToken);

        var writeFileTask = WriteWholeFileAtOnce(
            job: copyFileQueueJob,
            destinationWorkspace: destinationWorkspace,
            input: pipe.Reader,
            stoppingToken: cancellationToken);

        await Task.WhenAll(
            readFileTask,
            writeFileTask);
    }

    private async Task CopyFileInChunks(
        CopyFileQueueJob copyFileQueueJob,
        WorkspaceContext sourceWorkspace,
        WorkspaceContext destinationWorkspace,
        CancellationToken cancellationToken)
    {
        var pipe = new Pipe();

        var readFileTask = ReadFile(
            job: copyFileQueueJob,
            sourceWorkspace: sourceWorkspace,
            output: pipe.Writer,
            stoppingToken: cancellationToken);

        var writeFileTask = WriteFileInParts(
            job: copyFileQueueJob,
            destinationWorkspace: destinationWorkspace,
            input: pipe.Reader,
            cancellationToken: cancellationToken);

        await Task.WhenAll(
            readFileTask,
            writeFileTask);

        await destinationWorkspace.Storage.CompleteMultiPartUpload(
            bucketName: destinationWorkspace.BucketName,
            key: copyFileQueueJob.NewS3FileKey,
            uploadId: copyFileQueueJob.S3UploadId,
            partETags: writeFileTask.Result,
            cancellationToken: cancellationToken);
    }

    private CopyFileQueueJob? TryGetCopyFileQueueJobDetails(
        int copyFileQueueJobId)
    {
        using var connection = plikShareDb.OpenConnection();

        var details = connection
            .OneRowCmd(
                sql: @"
                    SELECT 
                        cfq_id,
                        cfq_file_upload_id,
                        cfq_source_workspace_id,
                        cfq_destination_workspace_id,

                        cfq_upload_algorithm,

                        fi_size_in_bytes,
                        fi_external_id,
                        fi_s3_key_secret_part,

                        fu_file_external_id,
                        fu_file_s3_key_secret_part,
                        fu_s3_upload_id,
                        fu_encryption_key_version,
                        fu_encryption_salt,
                        fu_encryption_nonce_prefix
                    FROM cfq_copy_file_queue
                    INNER JOIN fu_file_uploads
                        ON fu_id = cfq_file_upload_id
                    INNER JOIN fi_files
                        ON fi_id = cfq_file_id
                    WHERE 
                        cfq_id = $jobId
                ",
                readRowFunc: reader =>
                {
                    var encryptionKey = reader.GetByteOrNull(11);

                    return new CopyFileQueueJob
                    {
                        Id = reader.GetInt32(0),
                        FileUploadId = reader.GetInt32(1),
                        SourceWorkspaceId = reader.GetInt32(2),
                        TargetWorkspaceId = reader.GetInt32(3),
                        UploadAlgorithm = reader.GetEnum<UploadAlgorithm>(4),
                        FileSizeInBytes = reader.GetInt64(5),

                        SourceS3FileKey = new S3FileKey
                        {
                            FileExternalId = reader.GetExtId<FileExtId>(6),
                            S3KeySecretPart = reader.GetString(7),
                        },

                        NewS3FileKey = new S3FileKey
                        {
                            FileExternalId = reader.GetExtId<FileExtId>(8),
                            S3KeySecretPart = reader.GetString(9),
                        },
                        S3UploadId = reader.GetString(10),
                        NewFileEncryption = encryptionKey is null
                            ? new FileEncryption
                            {
                                EncryptionType = StorageEncryptionType.None
                            }
                            : new FileEncryption
                            {
                                EncryptionType = StorageEncryptionType.Managed,
                                Metadata = new FileEncryptionMetadata
                                {
                                    KeyVersion = encryptionKey.Value,
                                    Salt = reader.GetFieldValue<byte[]>(12),
                                    NoncePrefix = reader.GetFieldValue<byte[]>(13)
                                }
                            },
                    };
                })
            .WithParameter("$jobId", copyFileQueueJobId)
            .Execute();

        return details.IsEmpty 
            ? null 
            : details.Value;
    }

    private Task<UpdateResult> MarkCopyFileQueueJobAsUploading(
        int copyFileQueueJobId,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context =>
            {
                var result = context
                    .OneRowCmd(
                        sql: @"
                            UPDATE cfq_copy_file_queue
                            SET cfq_status = $uploadingStatus
                            WHERE cfq_id = $jobId
                            RETURNING cfq_id
                        ",
                        readRowFunc: reader => reader.GetInt32(0))
                    .WithParameter("$jobId", copyFileQueueJobId)
                    .WithEnumParameter("$uploadingStatus", CopyFileQueueStatus.Uploading)
                    .Execute();

                return result.IsEmpty 
                    ? UpdateResult.CopyFileQueueJobNotFound 
                    : UpdateResult.Ok;
            },
            cancellationToken: cancellationToken);
    }

    private static async Task ReadFile(
        CopyFileQueueJob job,
        WorkspaceContext sourceWorkspace,
        PipeWriter output,
        CancellationToken stoppingToken)
    {
        try
        {
            await FileReader.ReadFull(
                s3FileKey: job.SourceS3FileKey,
                fileSizeInBytes: job.FileSizeInBytes,
                workspace: sourceWorkspace,
                output: output,
                cancellationToken: stoppingToken);
        }
        finally
        {
            await output.CompleteAsync();
        }
    }

    private static async Task WriteWholeFileAtOnce(
        CopyFileQueueJob job,
        WorkspaceContext destinationWorkspace,
        PipeReader input,
        CancellationToken stoppingToken)
    {
        try
        {
            await FileWriter.Write(
                file: new FileToUploadDetails
                {
                    S3FileKey = job.NewS3FileKey,
                    SizeInBytes = job.FileSizeInBytes,
                    Encryption = job.NewFileEncryption,
                    S3UploadId = job.S3UploadId
                },
                part: FilePartDetails.First(
                    sizeInBytes: (int)job.FileSizeInBytes,
                    uploadAlgorithm: UploadAlgorithm.DirectUpload),
                workspace: destinationWorkspace,
                input: input,
                cancellationToken: stoppingToken);

        }
        finally
        {
            await input.CompleteAsync();
        }
    }

    private async Task<List<PartETag>> WriteFileInParts(
        CopyFileQueueJob job,
        WorkspaceContext destinationWorkspace,
        PipeReader input,
        CancellationToken cancellationToken)
    {
        var totalNumberOfParts = FileParts.GetTotalNumberOfParts(
            fileSizeInBytes: job.FileSizeInBytes,
            storageEncryptionType: destinationWorkspace.Storage.EncryptionType);

        var partNumber = 1;

        try
        {
            var eTags = new List<PartETag>();

            for (partNumber = 1; partNumber <= totalNumberOfParts; partNumber++)
            {
                var partSizeInBytes = FileParts.GetPartSizeInBytes(
                    fileSizeInBytes: job.FileSizeInBytes,
                    partNumber: partNumber,
                    storageEncryptionType: destinationWorkspace.Storage.EncryptionType);

                var result = await FileWriter.Write(
                    file: new FileToUploadDetails
                    {
                        S3FileKey = job.NewS3FileKey,
                        SizeInBytes = job.FileSizeInBytes,
                        Encryption = job.NewFileEncryption,
                        S3UploadId = job.S3UploadId
                    },
                    part: new FilePartDetails(
                        Number: partNumber,
                        SizeInBytes: partSizeInBytes,
                        UploadAlgorithm: UploadAlgorithm.MultiStepChunkUpload),
                    workspace: destinationWorkspace,
                    input: input,
                    cancellationToken: cancellationToken);

                var insertPartResult = await insertFileUploadPartQuery.Execute(
                    fileUploadId: job.FileUploadId,
                    partNumber: partNumber,
                    eTag: result.ETag,
                    cancellationToken: cancellationToken);

                if (insertPartResult.Code == InsertFileUploadPartQuery.ResultCode.FileUploadNotFound)
                {
                    throw new NotImplementedException(
                        $"FileUploadId#{job.FileUploadId} was not found in DB during upload of Part#{partNumber}");
                }

                eTags.Add(new PartETag(
                    partNumber: partNumber,
                    eTag: result.ETag));
            }

            return eTags;
        }
        catch (Exception e)
        {
            Log.Error(e,
                "Something went wrong during MultiStepChunkUpload of part number {PartNumber} of FileUpload#{FileUploadId}",
                partNumber,
                job.FileUploadId);

            throw;
        }
        finally
        {
            await input.CompleteAsync();
        }
    }

    private enum UpdateResult
    {
        Ok = 0,
        CopyFileQueueJobNotFound
    }
}