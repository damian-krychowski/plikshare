using PlikShare.Core.Encryption;
using PlikShare.Core.Queue;
using PlikShare.Core.UserIdentity;
using PlikShare.Core.Utils;
using PlikShare.Files.Id;
using PlikShare.Files.Metadata;
using PlikShare.Files.PreSignedLinks.RangeRequests;
using PlikShare.Files.UploadAttachment;
using PlikShare.Storages;
using PlikShare.Workspaces.Cache;
using Serilog;

namespace PlikShare.MediaProcessing.Generation;

public class ProcessImageQueueJobExecutor(
    WorkspaceCache workspaceCache,
    GetThumbnailSourceFileQuery getSourceFileQuery,
    TemporaryEncryptionStore temporaryEncryptionStore,
    IMasterDataEncryption masterEncryption,
    InsertAndFinalizeThumbnailQuery insertAndFinalizeThumbnailQuery,
    FfmpegService ffmpegService) : IQueueLongRunningJobExecutor
{
    private static readonly Serilog.ILogger Logger = Log.ForContext<ProcessImageQueueJobExecutor>();

    // How much of a video file to download for thumbnail extraction. Fast-start mp4 stores moov +
    // first samples in the first few MB; 8 MB covers virtually all 1080p consumer recordings and
    // most short 4K. Higher-bitrate / longer 4K sources may fail demux at this size — acceptable
    // trade-off vs hauling gigabytes. Pair with thumbnail filter's lower frame count (n=25) to
    // keep the in-RAM window proportional.
    private const long VideoRangeLimit = 8L * 1024 * 1024;

    public static string StaticJobType => ProcessImageQueueJobType.Value;
    public static int StaticPriority => QueueJobPriority.Normal;

    public string JobType => StaticJobType;
    public int Priority => StaticPriority;

    public async Task<QueueJobResult> Execute(
        string definitionJson,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        var definition = Json.Deserialize<ProcessImageQueueJobDefinition>(definitionJson)
            ?? throw new ArgumentException(
                $"Could not deserialize job definition: {definitionJson}");

        if (!ffmpegService.IsAvailable)
        {
            Logger.Warning(
                "Skipping image processing for batch of {Count} files — ffmpeg is not available. " +
                "Scheduling soft retry in case it gets installed.",
                definition.Files.Count);

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
                definition.Files.Count);

            foreach (var item in definition.Files)
                ReleasePackage(item.TempEncryptionKeyId);

            return QueueJobResult.Success;
        }

        var parentExternalIds = definition
            .Files
            .Select(f => f.ParentFileExternalId)
            .ToList();

        var parents = getSourceFileQuery.ExecuteBatch(
            parentExternalIds);

        var perFileResults = new List<ThumbnailGenerationResult.FileResult>(
            definition.Files.Count);

        var batchInsertItems = new List<InsertAndFinalizeThumbnailQuery.BatchItem>();

        try
        {
            foreach (var file in definition.Files)
            {
                var fileResult = await ProcessOneFile(
                    workspace: workspace,
                    file: file,
                    variants: definition.Variants,
                    parents: parents,
                    batchInsertItems: batchInsertItems,
                    cancellationToken: cancellationToken);

                perFileResults.Add(fileResult);
            }

            if (batchInsertItems.Count > 0)
            {
                await insertAndFinalizeThumbnailQuery.ExecuteBatch(
                    workspace: workspace,
                    uploader: new UserIdentity(
                        UserExternalId: definition.TriggeredByUserExternalId),
                    items: batchInsertItems,
                    correlationId: correlationId,
                    cancellationToken: cancellationToken);
            }
        }
        finally
        {
            foreach (var file in definition.Files)
                ReleasePackage(file.TempEncryptionKeyId);
        }

        return QueueJobResult.SuccessWithResult(
            Json.Serialize(new ThumbnailGenerationResult
            {
                Files = perFileResults
            }));
    }

    private async Task<ThumbnailGenerationResult.FileResult> ProcessOneFile(
        WorkspaceContext workspace,
        ProcessImageQueueJobDefinition.BatchItem file,
        List<ThumbnailVariant> variants,
        Dictionary<FileExtId, GetThumbnailSourceFileQuery.ThumbnailSourceFile> parents,
        List<InsertAndFinalizeThumbnailQuery.BatchItem> batchInsertItems,
        CancellationToken cancellationToken)
    {
        var failedVariants = new List<ThumbnailGenerationResult.FailedVariant>();
        var generatedVariants = new List<ThumbnailGenerationResult.GeneratedVariant>();

        if (!parents.TryGetValue(file.ParentFileExternalId, out var parent)
            || parent.WorkspaceId != workspace.Id)
        {
            Logger.Warning(
                "Parent File '{ParentFileExternalId}' not found in Workspace#{WorkspaceId} — skipping.",
                file.ParentFileExternalId,
                workspace.Id);

            return BuildFailedResult(file, variants, "Parent file not found.");
        }

        TemporaryEncryptionStore.Package? encryptionPackage = null;
        if (file.TempEncryptionKeyId is { } handleId)
        {
            encryptionPackage = temporaryEncryptionStore.TryRetrieve(
                handleId);

            if (encryptionPackage is null)
            {
                Logger.Warning(
                    "Temporary encryption package {HandleId} not found for File '{ParentFileExternalId}'.",
                    handleId,
                    file.ParentFileExternalId);

                return BuildFailedResult(file, variants, "Temporary encryption package not found.");
            }
        }
       
        string? tempFilePath = null;

        try
        {
            (var variantResults, tempFilePath) = await PrepareThumbnailVariants(
                workspace, 
                file, 
                variants,
                parent, 
                encryptionPackage, 
                cancellationToken);
            
            foreach (var result in variantResults)
            {
                if (result.Error is not null)
                {
                    Logger.Error(
                        "Failed to generate {Variant} thumbnail for File '{ParentFileExternalId}': {Error}",
                        result.Variant,
                        file.ParentFileExternalId,
                        result.Error);

                    failedVariants.Add(new ThumbnailGenerationResult.FailedVariant
                    {
                        Variant = result.Variant,
                        Error = Truncate(result.Error, maxLength: 500)
                    });

                    continue;
                }

                await using var thumbnail = result.Thumbnail!;

                try
                {
                    var descriptor = ThumbnailDescriptor.ForGeneratedWebp(
                        fileKey: workspace.GenerateFileKey(),
                        variant: result.Variant,
                        sizeInBytes: thumbnail.SizeInBytes);

                    var prepared = await BuildPreparedUpload(
                        workspace: workspace,
                        encryptionPackage: encryptionPackage,
                        thumbnail: descriptor,
                        thumbnailContent: thumbnail.Content,
                        cancellationToken: cancellationToken);

                    batchInsertItems.Add(new InsertAndFinalizeThumbnailQuery.BatchItem(
                        ParentFileExternalId: file.ParentFileExternalId,
                        Attachment: prepared.Attachment,
                        OldThumbnailFileIds: []));

                    generatedVariants.Add(new ThumbnailGenerationResult.GeneratedVariant
                    {
                        Variant = result.Variant,
                        Etag = prepared.Etag
                    });
                }
                catch (Exception ex)
                {
                    Logger.Error(
                        ex,
                        "Failed to upload {Variant} thumbnail for File '{ParentFileExternalId}'.",
                        result.Variant,
                        file.ParentFileExternalId);

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
                file.ParentFileExternalId);

            foreach (var variant in variants)
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

        return BuildFileResult(file, generatedVariants, failedVariants);
    }

    private async Task<VariantResults> PrepareThumbnailVariants(
        WorkspaceContext workspace, 
        ProcessImageQueueJobDefinition.BatchItem file, 
        List<ThumbnailVariant> variants,
        GetThumbnailSourceFileQuery.ThumbnailSourceFile parent,
        TemporaryEncryptionStore.Package? encryptionPackage,
        CancellationToken cancellationToken)
    {
        var parentDecryptionMode = encryptionPackage is null
            ? workspace.GetFileEncryptionMode(
                fileEncryptionMetadata: parent.EncryptionMetadata,
                workspaceEncryptionSession: null)
            : encryptionPackage.TakeDecryptionInput().ToEncryptionMode(
                masterEncryption);
        
        var isVideo = ContentTypeHelper.GetFileTypeFromExtension(file.Extension) == FileType.Video;

        if (isVideo)
        {
            var fileKey = new FileKey
            {
                FileExternalId = file.ParentFileExternalId,
                KeySecretPart = parent.KeySecretPart
            };

            var videoSource = parent.SizeInBytes <= VideoRangeLimit
                ? await workspace.Storage.DownloadFile(
                    fileDetails: new DownloadFileDetails(
                        FileKey: fileKey,
                        FileSizeInBytes: parent.SizeInBytes,
                        EncryptionMode: parentDecryptionMode),
                    bucketName: workspace.BucketName,
                    cancellationToken: cancellationToken)
                : await workspace.Storage.DownloadFileRange(
                    fileDetails: new DownloadFileRangeDetails(
                        Range: new BytesRange(
                            Start: 0,
                            End: VideoRangeLimit - 1),
                        FileKey: fileKey,
                        FileSizeInBytes: parent.SizeInBytes,
                        EncryptionMode: parentDecryptionMode),
                    bucketName: workspace.BucketName,
                    cancellationToken: cancellationToken);

            string? videoTempPath = null;

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
                variants: variants,
                cancellationToken: cancellationToken);

            return new VariantResults(
                Variants: variantResults,
                TempFilePath: videoTempPath);
        }

        await using var storageFile = await workspace.Storage.DownloadFile(
            fileDetails: new DownloadFileDetails(
                FileKey: new FileKey
                {
                    FileExternalId = file.ParentFileExternalId,
                    KeySecretPart = parent.KeySecretPart
                },
                FileSizeInBytes: parent.SizeInBytes,
                EncryptionMode: parentDecryptionMode),
            bucketName: workspace.BucketName,
            cancellationToken: cancellationToken);

        var results = await ffmpegService.GenerateThumbnails(
            writeSourceTo: (writer, ct) => storageFile.ReadTo(writer, ct),
            variants: variants,
            cancellationToken: cancellationToken);
        
        return new VariantResults(
            Variants: results,
            TempFilePath: null);
    }

    private async Task<PreparedUpload> BuildPreparedUpload(
        WorkspaceContext workspace,
        TemporaryEncryptionStore.Package? encryptionPackage,
        ThumbnailDescriptor thumbnail,
        Stream thumbnailContent,
        CancellationToken cancellationToken)
    {
        if (encryptionPackage is not null)
        {
            using var metadataSeed = encryptionPackage
                .TakeMetadataEncryptionSeed()
                .Unwrap(masterEncryption);

            var encryptionWire = encryptionPackage.TakeNextEncryptionInput();
            
            var etag = await thumbnail.UploadAndHash(
                workspace: workspace,
                content: thumbnailContent,
                encryptionMode: encryptionWire.ToEncryptionMode(
                    masterEncryption),
                cancellationToken: cancellationToken);

            var attachment = new InsertFileAttachmentQuery.AttachmentFile
            {
                ExternalId = thumbnail.FileKey.FileExternalId,
                KeySecretPart = thumbnail.FileKey.KeySecretPart,
                SizeInBytes = thumbnail.SizeInBytes,

                EncryptionMetadata = encryptionWire.ToMetadata(),

                ContentType = metadataSeed.ToEncryptableMetadata(
                    thumbnail.ContentType),

                Name = metadataSeed.ToEncryptableMetadata(
                    thumbnail.FileName),

                Extension = metadataSeed.ToEncryptableMetadata(
                    thumbnail.FileExtension),

                Metadata = metadataSeed.ToEncryptableMetadata(Json.Serialize<FileMetadata>(
                    new ThumbnailFileMetadata
                    {
                        Variant = thumbnail.Variant,
                        Etag = etag
                    }))
            };

            return new PreparedUpload(
                Etag: etag,
                Attachment: attachment);
        }
        else
        {
            var encryptionMetadata = workspace.GenerateFileEncryptionMetadata();

            var etag = await thumbnail.UploadAndHash(
                workspace: workspace,
                content: thumbnailContent,
                encryptionMode: workspace.GetFileEncryptionMode(
                    fileEncryptionMetadata: encryptionMetadata,
                    workspaceEncryptionSession: null),
                cancellationToken: cancellationToken);

            var attachment = new InsertFileAttachmentQuery.AttachmentFile
            {
                ExternalId = thumbnail.FileKey.FileExternalId,
                KeySecretPart = thumbnail.FileKey.KeySecretPart,
                SizeInBytes = thumbnail.SizeInBytes,

                EncryptionMetadata = encryptionMetadata,

                ContentType = NoMetadataEncryption.Prepare(
                    thumbnail.ContentType),

                Name = NoMetadataEncryption.Prepare(
                    thumbnail.FileName),

                Extension = NoMetadataEncryption.Prepare(
                    thumbnail.FileExtension),

                Metadata = NoMetadataEncryption.Prepare(Json.Serialize<FileMetadata>(
                    new ThumbnailFileMetadata
                    {
                        Variant = thumbnail.Variant,
                        Etag = etag
                    }))
            };

            return new PreparedUpload(
                Etag: etag,
                Attachment: attachment);

        }
    }

    private static ThumbnailGenerationResult.FileResult BuildFailedResult(
        ProcessImageQueueJobDefinition.BatchItem file,
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
        ProcessImageQueueJobDefinition.BatchItem file,
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

    private void ReleasePackage(Guid? handleId)
    {
        if (handleId is { } id)
            temporaryEncryptionStore.Remove(id);
    }

    private record PreparedUpload(
        string Etag,
        InsertFileAttachmentQuery.AttachmentFile Attachment);

    private record VariantResults(
        IReadOnlyList<ThumbnailVariantResult> Variants,
        string? TempFilePath);
}
