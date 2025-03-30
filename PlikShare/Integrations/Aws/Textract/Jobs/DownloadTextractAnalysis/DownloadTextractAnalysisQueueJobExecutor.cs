using System.IO.Pipelines;
using System.Text;
using Amazon.Textract;
using Amazon.Textract.Model;
using CommunityToolkit.HighPerformance;
using Microsoft.Data.Sqlite;
using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.Queue;
using PlikShare.Core.SQLite;
using PlikShare.Core.UserIdentity;
using PlikShare.Core.Utils;
using PlikShare.Files.Delete.QueueJob;
using PlikShare.Files.Id;
using PlikShare.Files.Metadata;
using PlikShare.Files.Records;
using PlikShare.Storages;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.FileReading;
using PlikShare.Uploads.Algorithm;
using PlikShare.Uploads.Cache;
using PlikShare.Uploads.Id;
using PlikShare.Uploads.Initiate;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.GetSize;
using PlikShare.Workspaces.UpdateCurrentSizeInBytes.QueueJob;
using Serilog;

namespace PlikShare.Integrations.Aws.Textract.Jobs.DownloadTextractAnalysis;

public class DownloadTextractAnalysisQueueJobExecutor(
    PlikShareDb plikShareDb,
    DbWriteQueue dbWriteQueue,
    TextractClientStore textractClientStore,
    WorkspaceCache workspaceCache,
    BulkInsertFileUploadQuery bulkInsertFileUploadQuery,
    GetWorkspaceSizeQuery getWorkspaceSizeQuery,
    IQueue queue,
    IClock clock,
    TextractResultTemporaryStore textractResultTemporaryStore) : IQueueLongRunningJobExecutor
{
    public const string TextractAnalysisRawResult = "textract-analysis-raw-result";
    public const string TextractMarkdown = "textract-markdown";

    public string JobType => DownloadTextractAnalysisQueueJobType.Value;
    public int Priority => QueueJobPriority.Normal;

    public async Task<QueueJobResult> Execute(
        string definitionJson, 
        Guid correlationId, 
        CancellationToken cancellationToken)
    {
        var definition = Json.Deserialize<DownloadTextractAnalysisQueueJobDefinition>(
            definitionJson);

        if (definition is null)
        {
            throw new ArgumentException(
                $"Job '{definitionJson}' cannot be parsed to correct '{nameof(DownloadTextractAnalysisQueueJobDefinition)}'");
        }

        var textractJob = TryGetTextractJob(
            textractJobId: definition.TextractJobId,
            textractTemporaryStoreId: definition.TextractTemporaryStoreId);

        if (textractJob is null)
        {
            Log.Warning("Cannot download Textract analysis because TextractJob#{TextractJobId} was not found. Download operation is skipped.",
                definition.TextractJobId);

            return QueueJobResult.Success;
        }

        var textractClient = textractClientStore.TryGetClient(
            textractJob.TextractIntegrationId);

        if (textractClient is null)
        {
            Log.Warning("Cannot download Textract analysis for TextractJob#{TextractJobId} because TextractClient#{TextractIntegrationId} was not found. Download operation is skipped",
                textractJob.Id,
                textractJob.TextractIntegrationId);

            return QueueJobResult.Success;
        }

        var originalFileWorkspace = await workspaceCache.TryGetWorkspace(
            workspaceId: textractJob.OriginalWorkspaceId,
            cancellationToken: cancellationToken);

        if (originalFileWorkspace is null)
        {
            Log.Warning("Cannot download Textract analysis for TextractJob#{TextractJobId} because Workspace#{WorkspaceId} was not found. Download operation is skipped",
                textractJob.Id,
                textractJob.OriginalWorkspaceId);

            return QueueJobResult.Success;
        }

        var textractWorkspace = await workspaceCache.TryGetWorkspace(
            workspaceId: textractJob.TextractWorkspaceId,
            cancellationToken: cancellationToken);

        if (textractWorkspace is null)
        {
            Log.Warning("Cannot download Textract analysis for TextractJob#{TextractJobId} because Workspace#{WorkspaceId} was not found. Download operation is skipped",
                textractJob.Id,
                textractJob.TextractWorkspaceId);

            return QueueJobResult.Success;
        }

        //todo: try to find more memory efficient way to prepare and store those textract result files.
        //todo: right now they are being created in memory - no matter their size
        //todo: and then saved int storage
        try
        {
            var (completeAnalysis, textractDerivedInfo) = await GetCompleteAnalysis(
                textractClient,
                textractJob,
                cancellationToken);

            var (markdownFileUploadId, jsonFileUploadId) = await SaveResultFiles(
                textractJob,
                originalFileWorkspace,
                completeAnalysis,
                textractDerivedInfo,
                cancellationToken);

            await FinalizeTextractJob(
                fileUploadIds: [
                    markdownFileUploadId,
                    jsonFileUploadId
                ],
                textractJob: textractJob,
                correlationId: correlationId,
                textractWorkspace: textractWorkspace,
                cancellationToken: cancellationToken);

            await workspaceCache.InvalidateEntry(
                workspaceId: originalFileWorkspace.Id,
                cancellationToken: cancellationToken);

            return QueueJobResult.Success;
        }
        catch (AmazonTextractException e)
        {
            Log.Error(e, "Downloading of Textract analysis for TextractJob#{TextractJobId} has failed on AWS Textract level.",
                textractJob.Id);

            throw;
        }
        catch (Exception e)
        {
            Log.Error(e, "Downloading of Textract analysis for TextractJob#{TextractJobId} has failed.",
                textractJob.Id);

            throw;
        }
    }

    private async Task<(int MarkdownFileUploadId, int JsonFileUploadId)> SaveResultFiles(
        TextractJob textractJob,
        WorkspaceContext originalFileWorkspace,
        TextractAnalysisResult textractAnalysis,
        TextractAnalysisDerivedInfo textractDerivedInfo,
        CancellationToken cancellationToken)
    {
        var jsonContent = Json.SerializeWithIndentation(
            textractAnalysis);

        var jsonContentBytes = Encoding.UTF8.GetBytes(
            jsonContent);

        var markdown = TextractMarkdownConverter.ToMarkdown(
            textractAnalysis: textractAnalysis,
            textractAnalysisDerivedInfo: textractDerivedInfo);

        var markdownContentBytes = Encoding.UTF8.GetBytes(
            markdown);

        var (markdownFile, fullJsonFile) = await InitiateResultFilesUpload(
            markdownFile: new ResultFile(
                Name: TextractMarkdown,
                Extension: ContentTypeHelper.Markdown,
                SizeInBytes: markdownContentBytes.Length),
            fullJsonFile: new ResultFile(
                Name: TextractAnalysisRawResult,
                Extension: ContentTypeHelper.Markdown,
                SizeInBytes: jsonContentBytes.Length),
            textractJob: textractJob,
            originalFileWorkspace: originalFileWorkspace,
            cancellationToken: cancellationToken);

        var writeMarkdownFileTask = WriteFile(
            originalFileWorkspace,
            markdownFile.InsertEntity,
            markdownContentBytes,
            cancellationToken);

        var writeJsonFileTask = WriteFile(
            originalFileWorkspace,
            fullJsonFile.InsertEntity,
            jsonContentBytes, 
            cancellationToken);

        await Task.WhenAll(
            writeMarkdownFileTask,
            writeJsonFileTask);

        return (
            MarkdownFileUploadId: markdownFile.FileUploadId, 
            JsonFileUploadId: fullJsonFile.FileUploadId
        );
    }

    private static async Task WriteFile(
        WorkspaceContext originalFileWorkspace,
        BulkInsertFileUploadQuery.InsertEntity fileInsertEntity,
        byte[] contentBytes, 
        CancellationToken cancellationToken)
    {
        await FileWriter.Write(
            file: new FileToUploadDetails
            {
                SizeInBytes = contentBytes.Length,
                Encryption = fileInsertEntity.EncryptionKeyVersion is null
                    ? new FileEncryption
                    {
                        EncryptionType = StorageEncryptionType.None
                    }
                    : new FileEncryption
                    {
                        EncryptionType = StorageEncryptionType.Managed,
                        Metadata = new FileEncryptionMetadata
                        {
                            KeyVersion = fileInsertEntity.EncryptionKeyVersion.Value,
                            NoncePrefix = fileInsertEntity.EncryptionNoncePrefix!,
                            Salt = fileInsertEntity.EncryptionSalt!
                        }
                    },
                S3FileKey = new S3FileKey
                {
                    FileExternalId = FileExtId.Parse(fileInsertEntity.FileExternalId),
                    S3KeySecretPart = fileInsertEntity.S3KeySecretPart
                },
                S3UploadId = fileInsertEntity.S3UploadId
            },
            part: FilePartDetails.First(
                sizeInBytes: contentBytes.Length,
                uploadAlgorithm: UploadAlgorithm.DirectUpload),
            workspace: originalFileWorkspace,
            input: PipeReader.Create(
                contentBytes.AsMemory().AsStream()),
            cancellationToken: cancellationToken);
    }

    private async Task<(TextractAnalysisResult, TextractAnalysisDerivedInfo)> GetCompleteAnalysis(
        TextractClient textractClient,
        TextractJob textractJob,
        CancellationToken cancellationToken)
    {
        var blocks = new List<Block>();
        var warnings = new List<Warning>();
        GetDocumentAnalysisResponse? firstResponse = null;
        string? nextToken = null;

        do
        {
            var response = await GetDocumentAnalysisResponse(
                textractClient, 
                textractJob, 
                nextToken, 
                cancellationToken);

            if (response.JobStatus != JobStatus.SUCCEEDED && response.JobStatus != JobStatus.PARTIAL_SUCCESS)
            {
                throw new InvalidOperationException(
                    $"TextractJob#{textractJob.Id} has wrong status '{response.JobStatus}'. " +
                    $"When analysis is being downloaded excepted statuses are '{JobStatus.SUCCEEDED}' or '{JobStatus.PARTIAL_SUCCESS}'");
            }

            firstResponse ??= response;

            blocks.AddRange(response.Blocks);
            if (response.Warnings != null)
            {
                warnings.AddRange(response.Warnings);
            }

            nextToken = response.NextToken;

        } while (!string.IsNullOrEmpty(nextToken));


        return TextractMappingExtensions.ToTextractAnalysisResult(
            textractDocumentMetadata: firstResponse.DocumentMetadata,
            textractBlocks: blocks,
            textractWarnings: warnings,
            textractModelVersion: firstResponse.AnalyzeDocumentModelVersion);
    }

    private async Task<GetDocumentAnalysisResponse> GetDocumentAnalysisResponse(
        TextractClient textractClient, 
        TextractJob textractJob,
        string? nextToken, 
        CancellationToken cancellationToken)
    {
        if (nextToken is not null)
        {
            return await textractClient.GetAnalysisResult(
                analysisJobId: textractJob.AnalysisJobId,
                nextToken: nextToken,
                cancellationToken: cancellationToken);
        }

        //it can be null because it could expire in the meantime or
        //service could have been reset and in-memory store was cleared
        var storedResult = textractResultTemporaryStore.TryGet(
            id: textractJob.TextractTemporaryStoreId);

        if (storedResult is not null)
            return storedResult;

        return await textractClient.GetAnalysisResult(
            analysisJobId: textractJob.AnalysisJobId,
            nextToken: null,
            cancellationToken: cancellationToken);
    }

    private async Task<(ResultFileUpload MarkdownFile, ResultFileUpload FullJsonFile)> InitiateResultFilesUpload(
        ResultFile markdownFile,
        ResultFile fullJsonFile,
        TextractJob textractJob,
        WorkspaceContext originalFileWorkspace,
        CancellationToken cancellationToken)
    {
        var markdownFileUploadToInsert = MapToBulkInsertEntity(
            markdownFile, 
            textractJob,
            originalFileWorkspace.Storage);

        var fullJsonFileUploadToInsert = MapToBulkInsertEntity(
            fullJsonFile,
            textractJob,
            originalFileWorkspace.Storage);

        var workspaceSize = getWorkspaceSizeQuery.Execute(
            workspace: originalFileWorkspace);

        var result = await bulkInsertFileUploadQuery.Execute(
            workspace: originalFileWorkspace,
            userIdentity: textractJob.UserIdentity,
            entities: [
                markdownFileUploadToInsert,
                fullJsonFileUploadToInsert
            ],
            newWorkspaceSizeInBytes: workspaceSize + markdownFile.SizeInBytes + fullJsonFile.SizeInBytes,
            cancellationToken: cancellationToken);

        var markdownFileUpload = result
            .FileUploads
            !.First(x => x.ExternalId.Equals(markdownFileUploadToInsert.FileUploadExternalId));

        var fullJsonFileUpload = result
            .FileUploads
            !.First(x => x.ExternalId.Equals(fullJsonFileUploadToInsert.FileUploadExternalId));

        return (
            MarkdownFile: new ResultFileUpload(
                FileUploadId: markdownFileUpload.Id,
                InsertEntity: markdownFileUploadToInsert
            ),

            FullJsonFile: new ResultFileUpload(
                FileUploadId: fullJsonFileUpload.Id,
                InsertEntity: fullJsonFileUploadToInsert)
        );
    }

    private static BulkInsertFileUploadQuery.InsertEntity MapToBulkInsertEntity(
        ResultFile file, 
        TextractJob textractJob, 
        IStorageClient storage)
    {
        var encryption = storage.GenerateFileEncryptionDetails();

        return new BulkInsertFileUploadQuery.InsertEntity
        {
            FileUploadExternalId = FileUploadExtId.NewId().Value,
            FileExternalId = FileExtId.NewId().Value,
            FolderId = null, //todo think where it should go
            FileName = file.Name,
            FileContentType = ContentTypeHelper.GetContentTypeFromExtension(file.Extension),
            FileExtension = file.Extension,
            FileSizeInBytes = file.SizeInBytes,
            S3KeySecretPart = storage.GenerateFileS3KeySecretPart(),

            S3UploadId = string.Empty,

            EncryptionKeyVersion = encryption.Metadata?.KeyVersion,
            EncryptionSalt = encryption.Metadata?.Salt,
            EncryptionNoncePrefix = encryption.Metadata?.NoncePrefix,

            ParentFileId = textractJob.OriginalFileId,
            FileMetadataBlob = Json.SerializeToBlob<FileMetadata>(new TextractResultFileMetadata
            {
                Features = textractJob.Features
            })
        };
    }

    private record ResultFile(
        string Name,
        string Extension,
        long SizeInBytes);

    private record ResultFileUpload(
        int FileUploadId,
        BulkInsertFileUploadQuery.InsertEntity InsertEntity);

    private TextractJob? TryGetTextractJob(
        int textractJobId,
        Guid textractTemporaryStoreId)
    {
        using var connection = plikShareDb.OpenConnection();

        var result = connection
            .OneRowCmd(
                sql: @"
                        SELECT 
                            itj_id,
                            itj_textract_analysis_job_id,
                            itj_textract_integration_id,
                            itj_original_workspace_id,
                            itj_original_file_id,
                            itj_textract_workspace_id,
                            itj_textract_file_id,
                            itj_owner_identity_type,
                            itj_owner_identity,
                            itj_definition
                        FROM itj_integrations_textract_jobs
                        WHERE 
                            itj_id = $textractJobId
                            AND itj_status = $downloadingResults
                        ORDER BY itj_id ASC                            
                    ",
                readRowFunc: reader => new TextractJob
                {
                    Id = reader.GetInt32(0),
                    AnalysisJobId = reader.GetString(1),
                    TextractIntegrationId = reader.GetInt32(2),
                    OriginalWorkspaceId = reader.GetInt32(3),
                    OriginalFileId = reader.GetInt32(4),
                    TextractWorkspaceId = reader.GetInt32(5),
                    TextractFileId = reader.GetInt32(6),

                    UserIdentity = new GenericUserIdentity(
                        IdentityType: reader.GetString(7),
                        Identity: reader.GetString(8)),

                    Features = reader
                        .GetFromJson<TextractJobDefinitionEntity>(9)
                        .Features,

                    TextractTemporaryStoreId = textractTemporaryStoreId
                })
            .WithEnumParameter("$downloadingResults", TextractJobStatus.DownloadingResults)
            .WithParameter("$textractJobId", textractJobId)
            .Execute();

        return result.IsEmpty
            ? null
            : result.Value;
    }

    private Task FinalizeTextractJob(
        int[] fileUploadIds,
        TextractJob textractJob,
        Guid correlationId,
        WorkspaceContext textractWorkspace,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: dbWriteContext =>
            {
                dbWriteContext.Connection.RegisterJsonArrayToBlobFunction();
                using var transaction = dbWriteContext.Connection.BeginTransaction();

                try
                {
                    var insertedFiles = dbWriteContext
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
                                RETURNING 
                                    fi_id,
                                    fi_name",
                            readRowFunc: reader => new
                            {
                                FileId = reader.GetInt32(0),
                                FileName = reader.GetString(1)
                            },
                            transaction: transaction)
                        .WithJsonParameter("$fileUploadIds", fileUploadIds)
                        .WithParameter("$createdAt", clock.UtcNow)
                        .Execute();
                    
                    var deletedItj = dbWriteContext
                        .OneRowCmd(
                            sql: @"
                                DELETE FROM itj_integrations_textract_jobs
                                WHERE itj_id = $textractJobId
                                RETURNING itj_id",
                            readRowFunc: reader => reader.GetInt32(0),
                            transaction: transaction)
                        .WithParameter("$textractJobId", textractJob.Id)
                        .ExecuteOrThrow();

                    //for now its not really needed, but i added it for the consistency
                    //in case some files will be uploaded as multichunk
                    var deletedFileUploadParts = dbWriteContext
                        .Cmd(
                            sql: @"
                                DELETE FROM fup_file_upload_parts
                                WHERE fup_file_upload_id IN (
                                    SELECT value FROM json_each($fileUploadIds)
                                )
                                RETURNING 
                                    fup_file_upload_id,
                                    fup_part_number                                            
                            ",
                            readRowFunc: reader => new {
                                FileUploadId = reader.GetInt32(0),
                                PartNumber = reader.GetInt32(1)
                            },
                            transaction: transaction)
                        .WithJsonParameter("$fileUploadIds", fileUploadIds)
                        .Execute();

                    var deletedFileUploadId = dbWriteContext
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

                    if (deletedFileUploadId.Count != fileUploadIds.Length)
                    {
                        //todo improve that log, tell expilicely which file uploads where not found
                        Log.Warning(
                            "Failed to delete file upload. FileUploads  where not found");
                    }
                    else
                    {
                        Log.Debug(
                            "Successfully deleted file upload. FileUploadId: {FileUploadId}",
                            deletedFileUploadId);
                    }

                    if (textractJob.TextractFileId != textractJob.OriginalFileId)
                    {
                        var deletedTextractFile = dbWriteContext
                            .OneRowCmd(
                                sql: @"
                                    DELETE FROM fi_files
                                    WHERE fi_id = $textractFileId
                                    RETURNING
                                        fi_external_id,
                                        fi_s3_key_secret_part
                                ",
                                readRowFunc: reader => new S3FileKey
                                {
                                    FileExternalId = reader.GetExtId<FileExtId>(0),
                                    S3KeySecretPart = reader.GetString(1)
                                },
                                transaction: transaction)
                            .WithParameter("$textractFileId", textractJob.TextractFileId)
                            .Execute();

                        if (!deletedTextractFile.IsEmpty)
                        {
                            EnqueueTextractFileCopyDeleteJob(
                                dbWriteContext: dbWriteContext,
                                textractWorkspace: textractWorkspace,
                                fileKey: deletedTextractFile.Value,
                                correlationId: correlationId,
                                transaction: transaction);
                        }
                    }
                    
                    queue.EnqueueWorkspaceSizeUpdateJob(
                        clock: clock,
                        workspaceId: textractJob.OriginalWorkspaceId,
                        correlationId: correlationId,
                        dbWriteContext: dbWriteContext,
                        transaction: transaction);

                    queue.EnqueueWorkspaceSizeUpdateJob(
                        clock: clock,
                        workspaceId: textractJob.TextractWorkspaceId,
                        correlationId: correlationId,
                        dbWriteContext: dbWriteContext,
                        transaction: transaction);

                    transaction.Commit();

                    Log.Debug(
                        "Successfully completed TextractJobId#{TextractJobId}.", textractJob.Id);

                }
                catch (Exception ex)
                {
                    transaction.Rollback();

                    Log.Error(ex,
                        "Error in finalizing TextractJobId#{TextractJobId}.",
                        textractJob.Id);

                    throw;
                }
            },
            cancellationToken: cancellationToken);
    }
    
    private QueueJobId EnqueueTextractFileCopyDeleteJob(
        DbWriteQueue.Context dbWriteContext,
        WorkspaceContext textractWorkspace,
        S3FileKey fileKey,
        Guid correlationId,
        SqliteTransaction transaction)
    {
        return queue.EnqueueOrThrow(
            correlationId: correlationId,
            jobType: DeleteS3FileQueueJobType.Value,
            definition: new DeleteS3FileQueueJobDefinition
            {
                StorageId = textractWorkspace.Storage.StorageId,
                BucketName = textractWorkspace.BucketName,
                FileExternalId = fileKey.FileExternalId,
                S3KeySecretPart = fileKey.S3KeySecretPart
            },
            executeAfterDate: clock.UtcNow.AddSeconds(10),
            debounceId: null,
            sagaId: null,
            dbWriteContext: dbWriteContext,
            transaction: transaction);
    }

    private class TextractJob
    {
        public required int Id { get; init; }
        public required string AnalysisJobId { get; init; }
        public required int TextractIntegrationId { get; init; }
        public required int OriginalWorkspaceId { get; init; }
        public required int OriginalFileId { get; init; }
        public required int TextractWorkspaceId { get; init; }
        public required int TextractFileId { get; init; }
        public required IUserIdentity UserIdentity { get; init; }
        public required TextractFeature[] Features { get; init; }
        
        public required Guid TextractTemporaryStoreId { get; init; }
    }
}

public class TextractAnalysisResult
{
    public required List<TextractBlock> Blocks { get; init; }
    public required TextractDocumentMetadata DocumentMetadata { get; init; }
    public required string ModelVersion { get; init; }
    public required List<TextractWarning> Warnings { get; init; }
}

public class TextractAnalysisDerivedInfo
{
    public required List<TextractTable> Tables { get; init; }
    public required List<TextractForm> Forms { get; init; }
    public required List<TextractPage> Pages { get; init; }
}

public class TextractBlock
{
    public required string Id { get; init; }
    public required string BlockType { get; init; } // PAGE, LINE, WORD, TABLE, CELL, SELECTION_ELEMENT, etc.
    public required float Confidence { get; init; }
    public required TextractGeometry Geometry { get; init; }
    public required List<TextractRelationship> Relationships { get; init; }
    public string? Text { get; init; }
    public string? TextType { get; init; }
    public List<string>? EntityTypes { get; init; }
    public string? SelectionStatus { get; init; }
    public int? RowIndex { get; init; }
    public int? ColumnIndex { get; init; }
    public int? RowSpan { get; init; }
    public int? ColumnSpan { get; init; }
    public int Page { get; init; }
}

public class TextractTable
{
    public required string TableId { get; init; }
    public required List<TextractTableCell> Cells { get; init; }
    public required int RowCount { get; init; }
    public required int ColumnCount { get; init; }
    public required TextractGeometry Geometry { get; init; }
    public float Confidence { get; init; }
}

public class TextractTableCell
{
    public required string Id { get; init; }
    public required int RowIndex { get; init; }
    public required int ColumnIndex { get; init; }
    public required int RowSpan { get; init; }
    public required int ColumnSpan { get; init; }
    public required TextractGeometry Geometry { get; init; }
    public string? Content { get; init; }
    public List<string> WordIds { get; init; } = new();
    public float Confidence { get; init; }
    public bool IsHeader { get; init; }
    public bool IsFooter { get; init; }
}

public class TextractForm
{
    public required string FieldId { get; init; }
    public required string Key { get; init; }
    public required string? Value { get; init; }
    public required TextractGeometry KeyGeometry { get; init; }
    public required TextractGeometry? ValueGeometry { get; init; }
    public float Confidence { get; init; }
}

public class TextractPage
{
    public required int PageNumber { get; init; }
    public required TextractGeometry Geometry { get; init; }
    public List<TextractTable> Tables { get; init; } = new();
    public List<TextractForm> Forms { get; init; } = new();
    public List<TextractBlock> Lines { get; init; } = new();
    public List<TextractBlock> Words { get; init; } = new();
}

public class TextractGeometry
{
    public required TextractBoundingBox BoundingBox { get; init; }
    public required TextractPolygon Polygon { get; init; }
}

public class TextractBoundingBox
{
    public required float Width { get; init; }
    public required float Height { get; init; }
    public required float Left { get; init; }
    public required float Top { get; init; }
}

public class TextractPolygon
{
    public required List<TextractPoint> Points { get; init; }
}

public class TextractPoint
{
    public required float X { get; init; }
    public required float Y { get; init; }
}

public class TextractRelationship
{
    public required string Type { get; init; } // CHILD, VALUE, MERGED_CELL, etc.
    public required List<string> Ids { get; init; }
}

public class TextractDocumentMetadata
{
    public required int Pages { get; init; }
}

public class TextractWarning
{
    public required string ErrorCode { get; init; }
    public required List<string> Pages { get; init; }
}

public static class TextractMappingExtensions
{
    public static (TextractAnalysisResult Analysis, TextractAnalysisDerivedInfo DerivedInfo) ToTextractAnalysisResult(
        DocumentMetadata textractDocumentMetadata,
        List<Block> textractBlocks,
        List<Warning> textractWarnings,
        string textractModelVersion)
    {
        var blocks = textractBlocks
            .Select(ToTextractBlock)
            .ToDictionary(b => b.Id);

        var analysis = new TextractAnalysisResult
        {
            Blocks = blocks.Values.ToList(),
            DocumentMetadata = ToTextractDocumentMetadata(textractDocumentMetadata),
            ModelVersion = textractModelVersion,
            Warnings = textractWarnings.Select(ToTextractWarning).ToList()
        };

        // Process blocks into structured data
        var derivedInfo = ProcessBlocks(blocks);

        return (analysis, derivedInfo);
    }

    private static TextractAnalysisDerivedInfo ProcessBlocks(
        Dictionary<string, TextractBlock> blocks)
    {
        var result = new TextractAnalysisDerivedInfo
        {
            Forms = [],
            Pages = [],
            Tables = []
        };

        var pageBlocks = blocks.Values
            .Where(b => b.BlockType == "PAGE")
            .OrderBy(b => b.Id);

        foreach (var pageBlock in pageBlocks.OrderBy(p => p.Page))
        {
            var page = new TextractPage
            {
                PageNumber = pageBlock.Page,
                Geometry = pageBlock.Geometry
            };

            var pageChildren = GetChildBlocks(pageBlock, blocks);

            // Process tables on this page
            var tableBlocks = pageChildren.Where(b => b.BlockType == "TABLE");
            foreach (var tableBlock in tableBlocks)
            {
                var table = ProcessTable(tableBlock, blocks);
                page.Tables.Add(table);
                result.Tables.Add(table);
            }

            // Process forms on this page
            var keyValueSets = pageChildren.Where(b => b.BlockType == "KEY_VALUE_SET");
            foreach (var kvSet in keyValueSets)
            {
                var form = ProcessForm(kvSet, blocks);
                if (form != null)
                {
                    page.Forms.Add(form);
                    result.Forms.Add(form);
                }
            }

            // Process lines and words
            page.Lines.AddRange(pageChildren.Where(b => b.BlockType == "LINE"));
            page.Words.AddRange(pageChildren.Where(b => b.BlockType == "WORD"));

            result.Pages.Add(page);
        }

        return result;
    }

    private static TextractTable ProcessTable(TextractBlock tableBlock, Dictionary<string, TextractBlock> blocks)
    {
        var cells = GetChildBlocks(tableBlock, blocks)
            .Where(b => b.BlockType == "CELL")
            .Select(cellBlock =>
            {
                var wordIds = GetChildBlocks(cellBlock, blocks)
                    .Where(b => b.BlockType == "WORD")
                    .Select(w => w.Id)
                    .ToList();

                return new TextractTableCell
                {
                    Id = cellBlock.Id,
                    RowIndex = cellBlock.RowIndex ?? 0,
                    ColumnIndex = cellBlock.ColumnIndex ?? 0,
                    RowSpan = cellBlock.RowSpan ?? 1,
                    ColumnSpan = cellBlock.ColumnSpan ?? 1,
                    Geometry = cellBlock.Geometry,
                    Content = cellBlock.Text,
                    WordIds = wordIds,
                    Confidence = cellBlock.Confidence,
                    // Assuming first row cells are headers - you might want to adjust this logic
                    IsHeader = (cellBlock.RowIndex ?? 0) == 0,
                    // You might want to add footer detection logic here
                    IsFooter = false
                };
            })
            .ToList();

        return new TextractTable
        {
            TableId = tableBlock.Id,
            Cells = cells,
            RowCount = cells.Max(c => c.RowIndex + c.RowSpan),
            ColumnCount = cells.Max(c => c.ColumnIndex + c.ColumnSpan),
            Geometry = tableBlock.Geometry,
            Confidence = tableBlock.Confidence
        };
    }

    private static TextractForm? ProcessForm(TextractBlock kvSetBlock, Dictionary<string, TextractBlock> blocks)
    {
        var keyRelation = kvSetBlock.Relationships
            .FirstOrDefault(r => r.Type == "CHILD");

        if (keyRelation == null) return null;

        var valueRelation = kvSetBlock.Relationships
            .FirstOrDefault(r => r.Type == "VALUE");

        var keyBlock = keyRelation.Ids
            .Select(id => blocks[id])
            .FirstOrDefault();

        var valueBlock = valueRelation?.Ids
            .Select(id => blocks[id])
            .FirstOrDefault();

        if (keyBlock == null) return null;

        return new TextractForm
        {
            FieldId = kvSetBlock.Id,
            Key = keyBlock.Text ?? "",
            Value = valueBlock?.Text,
            KeyGeometry = keyBlock.Geometry,
            ValueGeometry = valueBlock?.Geometry,
            Confidence = Math.Min(keyBlock.Confidence, valueBlock?.Confidence ?? 100)
        };
    }

    private static List<TextractBlock> GetChildBlocks(
        TextractBlock parent,
        Dictionary<string, TextractBlock> blocks)
    {
        var childRelation = parent.Relationships
            .FirstOrDefault(r => r.Type == "CHILD");

        if (childRelation == null)
            return new List<TextractBlock>();

        return childRelation.Ids
            .Where(id => blocks.ContainsKey(id))
            .Select(id => blocks[id])
            .ToList();
    }

    private static TextractBlock ToTextractBlock(Block block)
    {
        return new TextractBlock
        {
            Id = block.Id,
            BlockType = block.BlockType.ToString(),
            Confidence = block.Confidence,
            Geometry = ToTextractGeometry(block.Geometry),
            Text = block.Text,
            TextType = block.TextType?.Value,
            EntityTypes = block.EntityTypes?.ToList(),
            Relationships = block.Relationships?.Select(ToTextractRelationship).ToList() ?? new(),
            RowIndex = block.RowIndex,
            ColumnIndex = block.ColumnIndex,
            RowSpan = block.RowSpan,
            ColumnSpan = block.ColumnSpan,
            SelectionStatus = block.SelectionStatus?.Value,
            Page = block.Page
        };
    }

    private static TextractGeometry ToTextractGeometry(Geometry geometry)
    {
        return new TextractGeometry
        {
            BoundingBox = new TextractBoundingBox
            {
                Width = geometry.BoundingBox.Width,
                Height = geometry.BoundingBox.Height,
                Left = geometry.BoundingBox.Left,
                Top = geometry.BoundingBox.Top
            },
            Polygon = new TextractPolygon
            {
                Points = geometry.Polygon.Select(p => new TextractPoint
                {
                    X = p.X,
                    Y = p.Y
                }).ToList()
            }
        };
    }

    private static TextractRelationship ToTextractRelationship(Relationship relationship)
    {
        return new TextractRelationship
        {
            Type = relationship.Type.Value,
            Ids = relationship.Ids
        };
    }

    private static TextractDocumentMetadata ToTextractDocumentMetadata(DocumentMetadata metadata)
    {
        return new TextractDocumentMetadata
        {
            Pages = metadata.Pages
        };
    }

    private static TextractWarning ToTextractWarning(Warning warning)
    {
        return new TextractWarning
        {
            ErrorCode = warning.ErrorCode,
            Pages = warning.Pages.Select(p => p.ToString()).ToList()
        };
    }
}