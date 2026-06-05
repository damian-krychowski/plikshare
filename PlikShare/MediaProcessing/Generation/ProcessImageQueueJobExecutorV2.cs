using Microsoft.Data.Sqlite;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.Queue;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using PlikShare.Files.Id;
using PlikShare.Files.Metadata;
using PlikShare.Files.PreSignedLinks.RangeRequests;
using PlikShare.Files.Records;
using PlikShare.Storages;
using PlikShare.Storages.Encryption;
using PlikShare.Uploads.Algorithm;
using PlikShare.Workspaces.Cache;
using Serilog;
using System.Buffers.Text;
using System.IO.Pipelines;
using PlikShare.Core.UserIdentity;
using static PlikShare.MediaProcessing.InsertAndFinalizeThumbnailQuery;

namespace PlikShare.MediaProcessing.Generation;

public class ProcessImageQueueJobExecutorV2(
    PlikShareDb plikShareDb,
    WorkspaceCache workspaceCache,
    EphemeralKeyRing ephemeralKeyRing,
    InsertAndFinalizeThumbnailQuery insertAndFinalizeThumbnailQuery,
    FfmpegService ffmpegService) : IQueueLongRunningJobExecutor
{
    private static readonly Serilog.ILogger Logger = Log.ForContext<ProcessImageQueueJobExecutorV2>();

    // How much of a video file to download for thumbnail extraction. Fast-start mp4 stores moov +
    // first samples in the first few MB; 8 MB covers virtually all 1080p consumer recordings and
    // most short 4K. Higher-bitrate / longer 4K sources may fail demux at this size — acceptable
    // trade-off vs hauling gigabytes. Pair with thumbnail filter's lower frame count (n=25) to
    // keep the in-RAM window proportional.
    private const long VideoRangeLimit = 8L * 1024 * 1024;

    public static string StaticJobType => ProcessImageQueueJobTypeV2.Value;
    public static int StaticPriority => QueueJobPriority.Normal;

    public string JobType => StaticJobType;
    public int Priority => StaticPriority;

    public async Task<QueueJobResult> Execute(
        string definitionJson,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        var definition = Json.Deserialize<ProcessImageQueueJobDefinitionV2>(definitionJson)
            ?? throw new ArgumentException(
                $"Could not deserialize job definition: {definitionJson}");

        if (!ffmpegService.IsAvailable)
        {
            Logger.Warning(
                "Skipping image processing for batch of {Count} files — ffmpeg is not available. " +
                "Scheduling soft retry in case it gets installed.",
                definition.Files.Length);

            return QueueJobResult.NeedsRetry(
                maxAttempts: 3,
                delay: TimeSpan.FromMinutes(10));
        }

        var workspace = await workspaceCache.TryGetWorkspace(
            workspaceId: definition.WorkspaceId,
            cancellationToken: cancellationToken);

        if (workspace is null)
        {
            Logger.Warning(
                "Workspace#{WorkspaceId} not found — dropping image processing job for batch of {Count} files.",
                definition.WorkspaceId,
                definition.Files.Length);
            
            return QueueJobResult.Success;
        }

        var (parentFiles, existingThumbnails) = GetRelatedFiles(
            definition,
            workspace);

        var allNewThumbnails = new List<BulkInsertFileEntity>();
        var allOldThumbnailIds = new List<int>();
        var results = new List<OneFileResult>();

        foreach (var batchItem in definition.Files)
        {
            var fileResult = await ProcessOneFile(
                workspace: workspace,
                batchItem: batchItem,
                parentFiles: parentFiles,
                cancellationToken: cancellationToken);

            results.Add(fileResult);

            FindExistingThumbnailsToDelete(
                fileResult,
                existingThumbnails,
                batchItem,
                allOldThumbnailIds);

            allNewThumbnails.AddRange(
                fileResult.ThumbnailFileEntities);
        }

        if (allNewThumbnails.Count > 0)
        {
            await insertAndFinalizeThumbnailQuery.ExecuteBatch(
                workspace: workspace,
                uploader: new UserIdentity(definition.TriggeredByUserExternalId),
                items: allNewThumbnails,
                allOldThumbnailIds: allOldThumbnailIds,
                correlationId: correlationId,
                cancellationToken: cancellationToken);
        }

        return QueueJobResult.SuccessWithResult(
            Json.Serialize(new ThumbnailGenerationResult
            {
                Files = results.Select(r => r.Result).ToList()
            }));
    }

    private static void FindExistingThumbnailsToDelete(
        OneFileResult fileResult, 
        Dictionary<string, List<ExistingThumbnail>> existingThumbnails,
        ProcessImageQueueJobDefinitionV2.BatchItem batchItem, 
        List<int> allOldThumbnailIds)
    {
        var hasExistingThumbnails = existingThumbnails.TryGetValue(
            batchItem.ParentFileExternalId.Value,
            out var fileExistingThumbnails);

        if (!hasExistingThumbnails || fileExistingThumbnails is null)
            return;

        foreach (var generatedVariant in fileResult.Result.GeneratedVariants)
        {
            foreach (var fileExistingThumbnail in fileExistingThumbnails)
            {
                if (generatedVariant.Variant == fileExistingThumbnail.Variant)
                {
                    allOldThumbnailIds.Add(fileExistingThumbnail.Id);
                }
            }
        }
    }

    private async Task<OneFileResult> ProcessOneFile(
        WorkspaceContext workspace,
        ProcessImageQueueJobDefinitionV2.BatchItem batchItem,
        Dictionary<string, FileEntity> parentFiles,
        CancellationToken cancellationToken)
    {
        var allVariants = batchItem.GetVariants();

        if (!parentFiles.TryGetValue(batchItem.ParentFileExternalId.Value, out var parentFile))
        {
            var error = BuildFailedResult(
                file: batchItem,
                variants: allVariants,
                error: $"File with externalId '{batchItem.ParentFileExternalId}' was not found");

            return new OneFileResult(error, []);
        }

        var failedVariants = new List<ThumbnailGenerationResult.FailedVariant>();
        var generatedVariants = new List<ThumbnailGenerationResult.GeneratedVariant>();
        var readyThumbnailFiles = new List<BulkInsertFileEntity>();

        string? tempFilePath = null;
        
        try
        {
            (var variantResults, tempFilePath, var errorResult) = await PrepareThumbnailVariants(
                workspace: workspace,
                batchItem: batchItem, 
                parentFile: parentFile, 
                cancellationToken: cancellationToken);

            if (errorResult is not null)
                return new OneFileResult(errorResult, []);
            

            foreach (var result in variantResults!)
            {
                await using var thumbnail = result.Thumbnail!;

                if (result.Error is not null)
                {
                    Logger.Error(
                        "Failed to generate {Variant} thumbnail for File '{ParentFileExternalId}': {Error}",
                        result.Variant,
                        parentFile.ExternalId,
                        result.Error);

                    failedVariants.Add(new ThumbnailGenerationResult.FailedVariant
                    {
                        Variant = result.Variant,
                        Error = Truncate(result.Error, maxLength: 500)
                    });

                    continue;
                }

                var variantBatchItem = batchItem
                    .VariantItems
                    .First(x => x.Variant == result.Variant);
                
                var (details, error) = GetVariantFileEncryptionModeAndEncryptionSeed(
                    workspace: workspace,
                    variantItem: variantBatchItem,
                    parentFileExternalId: batchItem.ParentFileExternalId);

                if (error is not null)
                {
                    failedVariants.Add(new ThumbnailGenerationResult.FailedVariant
                    {
                        Variant = result.Variant,
                        Error = error
                    });

                    continue;
                }
                
                try
                {
                    var variant = variantBatchItem.Variant;
                    var (encryptionMode, encryptionMetadata, encryptionSeed) = details!;

                    using (encryptionSeed)
                    {
                        var fileKey = workspace.GenerateFileKey();

                        var etag = await UploadThumbnailAndHash(
                            workspace: workspace,
                            content: thumbnail.Content,
                            fileKey: fileKey,
                            fileSizeInBytes: thumbnail.SizeInBytes,
                            encryptionMode: encryptionMode,
                            cancellationToken: cancellationToken);

                        generatedVariants.Add(new ThumbnailGenerationResult.GeneratedVariant
                        {
                            Variant = result.Variant,
                            Etag = etag
                        });

                        readyThumbnailFiles.Add(new BulkInsertFileEntity
                        {
                            FileExternalId = fileKey.FileExternalId.Value,
                            KeySecretPart = fileKey.KeySecretPart,
                            FileSizeInBytes = thumbnail.SizeInBytes,

                            ParentFileId = parentFile.Id,
                            FolderId = parentFile.FolderId,

                            FileContentType = encryptionSeed
                                .DeriveNew()
                                .EncodeMetadata("image/webp"),

                            FileName = encryptionSeed
                                .DeriveNew()
                                .EncodeMetadata($"thumb-{variant.ToString().ToLowerInvariant()}"),

                            FileExtension = encryptionSeed
                                .DeriveNew()
                                .EncodeMetadata(".webp"),

                            FileMetadata = encryptionSeed
                                .DeriveNew()
                                .EncodeMetadata(Json.Serialize<FileMetadata>(
                                    new ThumbnailFileMetadata
                                    {
                                        Variant = variant,
                                        Etag = etag
                                    })),

                            EncryptionKeyVersion = encryptionMetadata?.KeyVersion,
                            EncryptionChainSalts = KeyDerivationChain.Serialize(encryptionMetadata?.ChainStepSalts),
                            EncryptionFormatVersion = encryptionMetadata?.FormatVersion,
                            EncryptionNoncePrefix = encryptionMetadata?.NoncePrefix,
                            EncryptionSalt = encryptionMetadata?.Salt
                        });
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(
                        ex,
                        "Failed to upload {Variant} thumbnail for File '{ParentFileExternalId}'.",
                        result.Variant,
                        batchItem.ParentFileExternalId);

                    failedVariants.Add(new ThumbnailGenerationResult.FailedVariant
                    {
                        Variant = result.Variant,
                        Error = Truncate(ex.Message, maxLength: 500)
                    });
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Logger.Error(
                ex,
                "Storage/ffmpeg failed for File '{ParentFileExternalId}'.",
                batchItem.ParentFileExternalId);

            foreach (var variant in allVariants)
            {
                if (failedVariants.All(f => f.Variant != variant)
                    && generatedVariants.All(g => g.Variant != variant))
                {
                    failedVariants.Add(new ThumbnailGenerationResult.FailedVariant
                    {
                        Variant = variant,
                        Error = Truncate(ex.Message, maxLength: 500)
                    });
                }
            }
        }
        finally
        {
            if (tempFilePath is not null)
            {
                try
                {
                    File.Delete(tempFilePath);
                }
                catch (Exception ex)
                {
                    Logger.Warning(
                        ex,
                        "Failed to delete video temp file '{TempPath}'.",
                        tempFilePath);
                }
            }
        }

        return new OneFileResult(
            Result: BuildFileResult(
                batchItem,
                generatedVariants,
                failedVariants),
            ThumbnailFileEntities: readyThumbnailFiles);
    }

    private (FileEncryptionMode? EncryptionMode, string? Error) GetParentFileEncryptionMode(
        WorkspaceContext workspace, 
        FileEntity file)
    {
        if (workspace.EncryptionType != StorageEncryptionType.Full)
        {
            var encryptionMode = workspace.GetFileEncryptionMode(
                fileEncryptionMetadata: file.EncryptionMetadata,
                workspaceEncryptionSession: null);

            return (encryptionMode, null);
        }
        
        var (seed, error) = ResolveEncryptionSeed(
            ephemeralSeed: file.EncryptionSeed,
            workspace: workspace,
            parentFileExternalId: file.ExternalId);

        if (error is not null)
            return (null, error);

        using (seed)
        {
            var fullEncryptionMode = seed!.ToFileEncryptionMode(
                metadata: file.EncryptionMetadata!);

            return (fullEncryptionMode, null);
        }
    }

    private VariantFileDetailsResult GetVariantFileEncryptionModeAndEncryptionSeed(
        WorkspaceContext workspace,
        ProcessImageQueueJobDefinitionV2.VariantItem variantItem,
        FileExtId parentFileExternalId)
    {
        if (workspace.EncryptionType != StorageEncryptionType.Full)
        {
            var metadata = workspace.GenerateFileEncryptionMetadata();

            var encryptionMode = workspace.GetFileEncryptionMode(
                fileEncryptionMetadata: metadata,
                workspaceEncryptionSession: null);

            return new VariantFileDetailsResult(
                Details: new VariantFileDetails(
                    EncryptionMode: encryptionMode,
                    EncryptionMetadata: metadata,
                    EncryptionSeed: NoMetadataEncryptionSeed.Instance),
                Error: null);
        }

        var (seed, error) = ResolveEncryptionSeed(
            ephemeralSeed: variantItem.EncryptionSeed,
            workspace: workspace,
            parentFileExternalId: parentFileExternalId);

        if (error is not null)
            return new VariantFileDetailsResult(
                Details: null,
                Error: error);

        var fileEncryptionMetadata = seed!.GenerateFileEncryptionMetadata();

        var fullEncryptionMode = seed.ToFileEncryptionMode(
            fileEncryptionMetadata);
        
        return new VariantFileDetailsResult(
            Details: new VariantFileDetails(
                EncryptionMode: fullEncryptionMode,
                EncryptionMetadata: fileEncryptionMetadata,
                EncryptionSeed: seed),
            Error: null);
    }

    private (FullEncryptionSeed? Seed, string? Error) ResolveEncryptionSeed(
        FullEncryptionSeedEphemeral? ephemeralSeed,
        WorkspaceContext workspace,
        FileExtId parentFileExternalId)
    {
        if (ephemeralSeed is null)
        {
            Logger.Error(
                "Full-encryption file '{ParentFileExternalId}' in workspace#{WorkspaceId} has no ephemeral encryption seed in the job definition.",
                parentFileExternalId,
                workspace.Id);

            return (null, "encryption seed missing for full-encryption file");
        }

        var status = ephemeralSeed.TryDecode(
            ephemeralKeyRing: ephemeralKeyRing,
            out var seed);

        if (status == EphemeralDecodeStatus.Ok && seed is not null)
            return (seed, null);

        if (status == EphemeralDecodeStatus.Expired)
        {
            Logger.Warning(
                "Ephemeral encryption key expired for full-encryption file '{ParentFileExternalId}' in workspace#{WorkspaceId} — the job is older than the key TTL or the service was restarted. Skipping thumbnail generation.",
                parentFileExternalId,
                workspace.Id);

            return (null, "encryption key no longer available");
        }

        Logger.Error(
            "Ephemeral encryption key could not be decoded ({Status}) for full-encryption file '{ParentFileExternalId}' in workspace#{WorkspaceId}.",
            status,
            parentFileExternalId,
            workspace.Id);

        return (null, "encryption key could not be decoded");
    }

    private async Task<VariantResults> PrepareThumbnailVariants(
        WorkspaceContext workspace, 
        ProcessImageQueueJobDefinitionV2.BatchItem batchItem, 
        FileEntity parentFile,
        CancellationToken cancellationToken)
    {
        var (fileEncryptionMode, error) = GetParentFileEncryptionMode(
            workspace: workspace,
            file: parentFile);

        if (error is not null)
        {
            return new VariantResults(
                Variants: null,
                TempFilePath: null,
                ErrorResult: BuildFailedResult(
                    batchItem,
                    batchItem.GetVariants(),
                    error
                ));
        }

        if (batchItem.IsVideo)
        {
            var fileKey = new FileKey
            {
                FileExternalId = batchItem.ParentFileExternalId,
                KeySecretPart = parentFile.KeySecretPart
            };

            var videoSource = parentFile.SizeInBytes <= VideoRangeLimit
                ? await workspace.Storage.DownloadFile(
                    fileDetails: new DownloadFileDetails(
                        FileKey: fileKey,
                        FileSizeInBytes: parentFile.SizeInBytes,
                        EncryptionMode: fileEncryptionMode!),
                    bucketName: workspace.BucketName,
                    cancellationToken: cancellationToken)
                : await workspace.Storage.DownloadFileRange(
                    fileDetails: new DownloadFileRangeDetails(
                        Range: new BytesRange(
                            Start: 0,
                            End: VideoRangeLimit - 1),
                        FileKey: fileKey,
                        FileSizeInBytes: parentFile.SizeInBytes,
                        EncryptionMode: fileEncryptionMode!),
                    bucketName: workspace.BucketName,
                    cancellationToken: cancellationToken);

            string? videoTempPath;

            await using (videoSource)
            {
                videoTempPath = Path.Combine(
                    Path.GetTempPath(),
                    $"plikshare-thumb-{Guid.NewGuid():N}");

                await using (var fs = File.Create(videoTempPath))
                    await videoSource.ReadTo(
                        System.IO.Pipelines.PipeWriter.Create(fs),
                        cancellationToken);
            }
            
            var variantResults = await ffmpegService.GenerateThumbnailsFromFile(
                filePath: videoTempPath,
                variants: batchItem.GetVariants(),
                cancellationToken: cancellationToken);

            return new VariantResults(
                Variants: variantResults,
                TempFilePath: videoTempPath,
                ErrorResult: null);
        }

        await using var storageFile = await workspace.Storage.DownloadFile(
            fileDetails: new DownloadFileDetails(
                FileKey: new FileKey
                {
                    FileExternalId = batchItem.ParentFileExternalId,
                    KeySecretPart = parentFile.KeySecretPart
                },
                FileSizeInBytes: parentFile.SizeInBytes,
                EncryptionMode: fileEncryptionMode!),
            bucketName: workspace.BucketName,
            cancellationToken: cancellationToken);

        var results = await ffmpegService.GenerateThumbnails(
            writeSourceTo: storageFile.ReadTo,
            variants: batchItem.GetVariants(),
            cancellationToken: cancellationToken);
        
        return new VariantResults(
            Variants: results,
            TempFilePath: null,
            ErrorResult: null);
    }


    private async Task<string> UploadThumbnailAndHash(
        WorkspaceContext workspace,
        Stream content,
        FileKey fileKey,
        long fileSizeInBytes,
        FileEncryptionMode encryptionMode,
        CancellationToken cancellationToken)
    {
        var hashingStream = new XxHashingReadStream(
            content);

        await workspace.UploadFilePart(
            input: PipeReader.Create(
                stream: hashingStream),
            uploadDetails: new UploadFilePartDetails(
                FileKey: fileKey,
                MultipartUploadId: null,
                FileSizeInBytes: fileSizeInBytes,
                Part: FilePart.First((int)fileSizeInBytes),
                UploadAlgorithm: UploadAlgorithm.DirectUpload,
                EncryptionMode: encryptionMode),
            cancellationToken: cancellationToken);

        Span<byte> hashBytes = stackalloc byte[16];
        hashingStream.Hash.GetCurrentHash(hashBytes);

        return Base64Url.EncodeToString(hashBytes);
    }

    private static ThumbnailGenerationResult.FileResult BuildFailedResult(
        ProcessImageQueueJobDefinitionV2.BatchItem file,
        List<ThumbnailVariant> variants,
        string error)
    {
        var failed = new List<ThumbnailGenerationResult.FailedVariant>(variants.Count);

        foreach (var variant in variants)
        {
            failed.Add(new ThumbnailGenerationResult.FailedVariant
            {
                Variant = variant,
                Error = error
            });
        }

        return BuildFileResult(file, [], failed);
    }

    private static ThumbnailGenerationResult.FileResult BuildFileResult(
        ProcessImageQueueJobDefinitionV2.BatchItem file,
        List<ThumbnailGenerationResult.GeneratedVariant> generatedVariants,
        List<ThumbnailGenerationResult.FailedVariant> failedVariants) => new()
    {
        ParentFileExternalId = file.ParentFileExternalId,
        GeneratedVariants = generatedVariants,
        FailedVariants = failedVariants
    };

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength
            ? value
            : value[..maxLength];
    }

    private record VariantResults(
        IReadOnlyList<ThumbnailVariantResult>? Variants,
        string? TempFilePath,
        ThumbnailGenerationResult.FileResult? ErrorResult);
    private record OneFileResult(
        ThumbnailGenerationResult.FileResult Result,
        List<BulkInsertFileEntity> ThumbnailFileEntities);

    private record VariantFileDetails(
        FileEncryptionMode EncryptionMode,
        FileEncryptionMetadata? EncryptionMetadata,
        IMetadataEncryptionSeed EncryptionSeed);

    private record VariantFileDetailsResult(
        VariantFileDetails? Details,
        string? Error);

    private (Dictionary<string, FileEntity> ParentFiles, Dictionary<string, List<ExistingThumbnail>> ExistingThumbnails) GetRelatedFiles(
        ProcessImageQueueJobDefinitionV2 definition,
        WorkspaceContext workspace)
    {
        var encryptionKeys = definition
            .Files
            .ToDictionary(
                definitionFile => definitionFile.ParentFileExternalId.Value,
                definitionFile => definitionFile.EncryptionSeed);

        var parentExternalIds = encryptionKeys.Keys.ToList();

        using var connection = plikShareDb.OpenConnection();

        var parentFiles = connection
            .AggregateRows(
                sql: """
                     SELECT
                         fi_external_id,
                         fi_id,
                         fi_key_secret_part,
                         fi_size_in_bytes,
                         fi_folder_id,
                         fi_encryption_key_version,
                         fi_encryption_salt,
                         fi_encryption_nonce_prefix,
                         fi_encryption_chain_salts,
                         fi_encryption_format_version
                     FROM fi_files
                     WHERE fi_external_id IN (
                         SELECT value FROM json_each($externalIds)
                     )
                     AND fi_deleted_at IS NULL   
                     AND fi_workspace_id = $workspaceId
                     """,
                seed: new Dictionary<string, FileEntity>(),
                aggregateRowFunc: (files, reader) =>
                {
                    var externalId = reader.GetExtId<FileExtId>(0);

                    files[externalId.Value] = new FileEntity
                    {
                        ExternalId = externalId,
                        Id = reader.GetInt32(1),
                        KeySecretPart = reader.GetString(2),
                        SizeInBytes = reader.GetInt64(3),
                        FolderId = reader.GetInt32OrNull(4),

                        EncryptionMetadata = reader.GetByteOrNull(5) is { } keyVersion
                            ? new FileEncryptionMetadata
                            {
                                KeyVersion = keyVersion,
                                Salt = reader.GetFieldValue<byte[]>(6),
                                NoncePrefix = reader.GetFieldValue<byte[]>(7),
                                ChainStepSalts = KeyDerivationChain.Deserialize(
                                    reader.GetFieldValueOrNull<byte[]>(8)),
                                FormatVersion = reader.GetByteOrNull(9) ?? 1
                            }
                            : null,

                        EncryptionSeed = encryptionKeys[externalId.Value]
                    };

                    return files;
                })
            .WithJsonParameter("$externalIds", parentExternalIds)
            .WithParameter("$workspaceId", workspace.Id)
            .Execute();

        var existingThumbnails = GetExistingThumbnails(
            workspace, 
            parentExternalIds, 
            connection);

        return (parentFiles, existingThumbnails);
    }

    private static Dictionary<string, List<ExistingThumbnail>> GetExistingThumbnails(
        WorkspaceContext workspace, 
        List<string> parentExternalIds, 
        SqliteConnection connection)
    {
        if (workspace.EncryptionType == StorageEncryptionType.Full)
            return [];
        
        // Old-duplicate cleanup is wired only for workspaces whose thumbnail metadata is
        // readable here WITHOUT a live WorkspaceEncryptionSession — None/Managed, where the
        // variant lives in plaintext fi_metadata. Full-encryption metadata can't be decoded
        // in the worker (the session never crosses the queue boundary), so we skip dedup for
        // those and let duplicates accumulate / be reaped elsewhere.
        return connection
            .AggregateRows(
                sql: """
                     SELECT
                         child_fi.fi_id,
                         child_fi.fi_size_in_bytes,
                         child_fi.fi_metadata,
                         parent_fi.fi_external_id
                     FROM fi_files AS child_fi
                     INNER JOIN fi_files AS parent_fi
                         ON parent_fi.fi_id = child_fi.fi_parent_file_id
                     WHERE
                         parent_fi.fi_external_id IN (
                            SELECT value FROM json_each($parentExternalIds)
                         ) 
                         AND parent_fi.fi_workspace_id = $workspaceId
                         AND parent_fi.fi_deleted_at IS NULL
                         AND child_fi.fi_workspace_id = $workspaceId
                         AND child_fi.fi_deleted_at IS NULL
                         AND child_fi.fi_is_upload_completed = TRUE
                         AND child_fi.fi_metadata IS NOT NULL
                     ORDER BY child_fi.fi_id DESC
                     """,
                seed: new Dictionary<string, List<ExistingThumbnail>>(),
                aggregateRowFunc: (acc, reader) =>
                {
                    var metadataJson = reader.DecodeEncryptableBlobOrNull(
                        2,
                        workspaceEncryptionSession: null);

                    if (metadataJson is null)
                        return acc;

                    var metadata = Json.Deserialize<FileMetadata>(
                        metadataJson);

                    if (metadata is not ThumbnailFileMetadata thumbnailMetadata)
                        return acc;

                    var parentFileExternalId = reader.GetString(3);

                    if (!acc.TryGetValue(parentFileExternalId, out var thumbnails))
                    {
                        thumbnails = [];
                        acc[parentFileExternalId] = thumbnails;
                    }

                    thumbnails.Add(new ExistingThumbnail
                    {
                        Id = reader.GetInt32(0),
                        Variant = thumbnailMetadata.Variant
                    });

                    return acc;
                })
            .WithJsonParameter("$parentExternalIds", parentExternalIds)
            .WithParameter("$workspaceId", workspace.Id)
            .Execute();
    }
    
    private class FileEntity
    {
        public required int Id { get; init; }
        public required FileExtId ExternalId { get; init; }
        public required int? FolderId { get; init; }
        public required string KeySecretPart { get; init; }
        public required long SizeInBytes { get; init; }
        public required FileEncryptionMetadata? EncryptionMetadata { get; init; }

        public required FullEncryptionSeedEphemeral? EncryptionSeed { get; init; }
    }

    private class ExistingThumbnail
    {
        public required int Id { get; init; }
        public required ThumbnailVariant Variant { get; init; }
    }
}
