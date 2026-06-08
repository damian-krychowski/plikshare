using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Queue;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using PlikShare.Files.Id;
using PlikShare.Storages;
using PlikShare.Workspaces.Cache;
using Serilog;

namespace PlikShare.Uploads.CompleteFileUpload.QueueJob;

public class CompleteMultipartUploadQueueJobExecutor(
    StorageClientStore storageClientStore,
    PlikShareDb plikShareDb,
    WorkspaceCache workspaceCache,
    MarkFileAsUploadedAndDeleteUploadQuery markFileAsUploadedAndDeleteUploadQuery) : IQueueNormalJobExecutor
{
    private readonly Serilog.ILogger _logger = Log.ForContext<CompleteMultipartUploadQueueJobExecutor>();
    public static string StaticJobType => CompleteMultipartUploadQueueJobType.Value;
    public static int StaticPriority => QueueJobPriority.High;

    public string JobType => StaticJobType;
    public int Priority => StaticPriority;

    public async Task<QueueJobResult> Execute(
        string definitionJson,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        _logger.Debug(
            "Starting multipart upload completion job. Definition: {@Definition}",
            definitionJson);

        var definition = Json.Deserialize<CompleteFileUploadQueueJobDefinition>(
            definitionJson);

        if (definition is null)
        {
            _logger.Error(
                "Failed to parse job definition. Definition: {Definition}",
                definitionJson);

            throw new ArgumentException(
                $"Job '{definitionJson}' cannot be parsed to correct '{nameof(CompleteFileUploadQueueJobDefinition)}'");
        }

        var (fileUpload, parts) = TryGetFileUploadDetails(
            fileUploadId: definition.FileUploadId);

        if (fileUpload is null)
        {
            _logger.Warning(
                "FileUpload not found. FileUploadId: {FileUploadId}",
                definition.FileUploadId);

            return QueueJobResult.Success;
        }

        _logger.Debug(
            "Retrieved file upload details. FileUploadId: {FileUploadId}, FileExternalId: {FileExternalId}, PartsCount: {PartsCount}",
            fileUpload.Id,
            fileUpload.FileExternalId,
            parts.Count);

        if (!storageClientStore.TryGetClient(fileUpload.StorageId, out var storage))
        {
            Log.Warning("Could not complete multipart upload of file '{FileExternalId}' because Storage#{StorageId} was not found. Marking the queue job as completed.",
                fileUpload.FileExternalId,
                fileUpload.StorageId);

            return QueueJobResult.Success;
        }

        try
        {
            await storage.CompleteMultiPartUpload(
                bucketName: fileUpload.BucketName,
                key: new FileKey
                {
                    FileExternalId = fileUpload.FileExternalId,
                    KeySecretPart = fileUpload.KeySecretPart
                },
                uploadId: fileUpload.MultipartUploadId,
                partETags: parts,
                cancellationToken: cancellationToken);

            _logger.Information(
                "Successfully completed multipart upload. FileUploadId: {FileUploadId}, BucketName: {BucketName}",
                fileUpload.Id,
                fileUpload.BucketName);

            var workspace = await workspaceCache.TryGetWorkspace(
                workspaceId: fileUpload.WorkspaceId,
                cancellationToken: cancellationToken);

            if (workspace is null)
            {
                Log.Warning(
                    "Could not run post-completion handlers for file '{FileExternalId}' because Workspace#{WorkspaceId} was not found.",
                    fileUpload.FileExternalId,
                    fileUpload.WorkspaceId);

                return QueueJobResult.Success;
            }

            return QueueJobResult.SuccessWithDbWrite(
                dbWrite: (dbWriteContext, transaction) => markFileAsUploadedAndDeleteUploadQuery.Execute(
                    dbWriteContext: dbWriteContext,
                    transaction: transaction,
                    workspace: workspace,
                    fileUploadId: fileUpload.Id,
                    fileExternalId: fileUpload.FileExternalId,
                    encryptionSeed: definition.EncryptionSeed,
                    correlationId: correlationId));
        }
        catch (Exception ex)
        {
            _logger.Error(
                ex,
                "Failed to complete multipart upload. CorrelationId: {CorrelationId}, FileUploadId: {FileUploadId}, BucketName: {BucketName}",
                correlationId,
                fileUpload.Id,
                fileUpload.BucketName);

            throw;
        }
    }

    private (FileUploadDetails? Details, List<UploadedFilePart> Parts) TryGetFileUploadDetails(
        int fileUploadId)
    {
        _logger.Debug("Retrieving file upload details. FileUploadId: {FileUploadId}", fileUploadId);

        using var connection = plikShareDb.OpenConnection();

        var result = connection
            .OneRowCmd(
                sql: """
                     SELECT
                         fu_file_external_id,
                         fu_file_key_secret_part,
                         fu_multipart_upload_id,
                         w_bucket_name,
                         w_storage_id,
                         fu_workspace_id
                     FROM fu_file_uploads
                     INNER JOIN w_workspaces
                         ON w_id = fu_workspace_id
                     WHERE
                         fu_id = $fileUploadId
                     LIMIT 1
                     """,
                readRowFunc: reader => new FileUploadDetails(
                    Id: fileUploadId,
                    FileExternalId: reader.GetExtId<FileExtId>(0),
                    KeySecretPart: reader.GetString(1),
                    MultipartUploadId: reader.GetString(2),
                    BucketName: reader.GetString(3),
                    StorageId: reader.GetInt32(4),
                    WorkspaceId: reader.GetInt32(5)))
            .WithParameter("$fileUploadId", fileUploadId)
            .Execute();

        if (result.IsEmpty)
        {
            _logger.Warning("File upload details not found. FileUploadId: {FileUploadId}", fileUploadId);

            return (null, []);
        }

        var parts = connection
            .Cmd(
                sql: """
                     SELECT fup_part_number, fup_etag
                     FROM fup_file_upload_parts
                     WHERE fup_file_upload_id = $fileUploadId
                     """,
                readRowFunc: reader => new UploadedFilePart(
                    PartNumber: reader.GetInt32(0),
                    ETag: reader.GetStringOrNull(1)))
            .WithParameter("$fileUploadId", fileUploadId)
            .Execute();

        _logger.Debug(
            "Retrieved file upload details and parts. FileUploadId: {FileUploadId}, FileExternalId: {FileExternalId}, PartsCount: {PartsCount}",
            fileUploadId,
            result.Value.FileExternalId,
            parts.Count);

        return (result.Value, parts);
    }

    private record FileUploadDetails(
        int Id,
        FileExtId FileExternalId,
        string KeySecretPart,
        string MultipartUploadId,
        string BucketName,
        int StorageId,
        int WorkspaceId);
}
