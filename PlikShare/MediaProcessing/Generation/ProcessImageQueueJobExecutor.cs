using PlikShare.Core.Encryption;
using PlikShare.Core.Queue;
using PlikShare.Core.UserIdentity;
using PlikShare.Core.Utils;
using PlikShare.Files.Id;
using PlikShare.Files.Metadata;
using PlikShare.Files.PreSignedLinks.RangeRequests;
using PlikShare.Storages;
using PlikShare.Storages.FileReading;
using PlikShare.Workspaces.Cache;
using Serilog;

namespace PlikShare.MediaProcessing.Generation;

/// <summary>
/// Background worker that takes a BATCH of parent files (1..N from <see cref="ProcessImageQueueJobDefinition.Files"/>),
/// decrypts each through its own temporary encryption session, runs ffmpeg per variant, and uploads
/// every generated WebP. The DB inserts for ALL variants of ALL files in the batch land in ONE
/// <see cref="DbWriteQueue"/> transaction (via <see cref="InsertAndFinalizeThumbnailQuery.ExecuteBatch"/>) —
/// cutting per-file DbWriteQueue contention.
///
/// Failure handling:
/// <list type="bullet">
///   <item>Workspace gone → drop the whole job (Success).</item>
///   <item>ffmpeg unavailable → soft retry the whole job.</item>
///   <item>Single file's temp key gone, parent gone, ffmpeg fail, storage fail — recorded as
///         that file's failure inside the batch result; the rest of the batch continues.</item>
/// </list>
/// </summary>
public class ProcessImageQueueJobExecutor(
    WorkspaceCache workspaceCache,
    GetThumbnailSourceFileQuery getSourceFileQuery,
    GetThumbnailsQuery getThumbnailsQuery,
    TemporaryWorkspaceEncryptionKeyStore keyStore,
    UploadFileThumbnailOperation uploadFileThumbnailOperation,
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
                ReleaseKey(item.TempEncryptionKeyId);

            return QueueJobResult.Success;
        }

        var uploader = new UserIdentity(
            UserExternalId: definition.TriggeredByUserExternalId);

        // Batched parent lookup — one SELECT IN (json_each(...)) for the whole batch instead of
        // one SELECT per file.
        var parentExternalIds = definition
            .Files
            .Select(f => f.ParentFileExternalId)
            .ToList();

        var parents = getSourceFileQuery.ExecuteBatch(parentExternalIds);

        var perFileResults = new List<ThumbnailGenerationResult.FileResult>(definition.Files.Count);
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

            // ONE DbWriteQueue tx for every variant of every file in the batch.
            if (batchInsertItems.Count > 0)
            {
                await insertAndFinalizeThumbnailQuery.ExecuteBatch(
                    workspace: workspace,
                    uploader: uploader,
                    items: batchInsertItems,
                    correlationId: correlationId,
                    cancellationToken: cancellationToken);

                // Per-item ParentNotFound (race between storage upload and insert) is logged inside
                // ExecuteBatch and surfaces only as a missing fi_files row. The result JSON keeps the
                // upbeat "generated" entry for that variant — frontend reads the truth from fresh DB.
            }
        }
        finally
        {
            foreach (var file in definition.Files)
                ReleaseKey(file.TempEncryptionKeyId);
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

            foreach (var variant in variants)
            {
                failedVariants.Add(new ThumbnailGenerationResult.FailedVariant
                {
                    Variant = variant,
                    Error = "Parent file not found."
                });
            }

            return BuildFileResult(file, generatedVariants, failedVariants);
        }

        WorkspaceEncryptionSession? session = null;

        if (file.TempEncryptionKeyId is { } keyId)
        {
            session = keyStore.TryRetrieve(keyId);

            if (session is null)
            {
                Logger.Warning(
                    "Temporary workspace encryption key {KeyId} not found for File '{ParentFileExternalId}'.",
                    keyId,
                    file.ParentFileExternalId);

                foreach (var variant in variants)
                {
                    failedVariants.Add(new ThumbnailGenerationResult.FailedVariant
                    {
                        Variant = variant,
                        Error = "Temporary encryption key not found."
                    });
                }

                return BuildFileResult(file, generatedVariants, failedVariants);
            }
        }

        // One existing-thumbnails read per parent file (not per variant) — passed into each
        // Prepare call below as a precomputed list.
        var existingThumbnails = getThumbnailsQuery.Execute(
            workspace: workspace,
            parentFileExternalId: file.ParentFileExternalId,
            workspaceEncryptionSession: session);

        var encryptionMode = parent.EncryptionMetadata.ToEncryptionMode(
            workspaceEncryptionSession: session,
            storageClient: workspace.Storage);

        // Video sources route through a temp file (seekable disk) instead of stdin. mp4 with a
        // moov-at-end blows up memory on non-seekable input; on a real file ffmpeg seeks freely.
        // Plus we only pull the FIRST ~VideoRangeLimit bytes via DownloadFileRange — enough for
        // moov + first samples on fast-start mp4, regardless of full file size (so a 4 GB DSLR
        // recording downloads ~32 MB, not 4 GB). Non-fast-start mp4 (moov at end) will fail to
        // demux and that variant is recorded as failed — acceptable trade-off vs hauling gigabytes.
        var isVideo = ContentTypeHelper.GetFileTypeFromExtension(file.Extension) == FileType.Video;
        string? videoTempPath = null;

        try
        {
            IReadOnlyList<VariantResult> results;

            if (isVideo)
            {
                var fileKey = new FileKey
                {
                    FileExternalId = file.ParentFileExternalId,
                    KeySecretPart = parent.KeySecretPart
                };

                // Files that fit entirely within the range cap go through the plain DownloadFile —
                // ranged read adds an offset/length round-trip + encryption-segment alignment work
                // that's pure overhead when we'd take the whole file anyway.
                IStorageFile videoSource = parent.SizeInBytes <= VideoRangeLimit
                    ? await workspace.Storage.DownloadFile(
                        fileDetails: new DownloadFileDetails(
                            FileKey: fileKey,
                            FileSizeInBytes: parent.SizeInBytes,
                            EncryptionMode: encryptionMode),
                        bucketName: workspace.BucketName,
                        cancellationToken: cancellationToken)
                    : await workspace.Storage.DownloadFileRange(
                        fileDetails: new DownloadFileRangeDetails(
                            Range: new BytesRange(
                                Start: 0,
                                End: VideoRangeLimit - 1),
                            FileKey: fileKey,
                            FileSizeInBytes: parent.SizeInBytes,
                            EncryptionMode: encryptionMode),
                        bucketName: workspace.BucketName,
                        cancellationToken: cancellationToken);

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

                results = await ffmpegService.GenerateThumbnailsFromFile(
                    filePath: videoTempPath,
                    variants: variants,
                    cancellationToken: cancellationToken);
            }
            else
            {
                await using var storageFile = await workspace.Storage.DownloadFile(
                    fileDetails: new DownloadFileDetails(
                        FileKey: new FileKey
                        {
                            FileExternalId = file.ParentFileExternalId,
                            KeySecretPart = parent.KeySecretPart
                        },
                        FileSizeInBytes: parent.SizeInBytes,
                        EncryptionMode: encryptionMode),
                    bucketName: workspace.BucketName,
                    cancellationToken: cancellationToken);

                results = await ffmpegService.GenerateThumbnails(
                    writeSourceTo: (writer, ct) => storageFile.ReadTo(writer, ct),
                    variants: variants,
                    cancellationToken: cancellationToken);
            }

            foreach (var result in results)
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
                        externalId: FileExtId.NewId(),
                        variant: result.Variant,
                        sizeInBytes: thumbnail.SizeInBytes);

                    var prepared = await uploadFileThumbnailOperation.Prepare(
                        workspace: workspace,
                        parentFileExternalId: file.ParentFileExternalId,
                        thumbnail: descriptor,
                        content: thumbnail.Content,
                        workspaceEncryptionSession: session,
                        existingThumbnails: existingThumbnails,
                        cancellationToken: cancellationToken);

                    batchInsertItems.Add(new InsertAndFinalizeThumbnailQuery.BatchItem(
                        ParentFileExternalId: file.ParentFileExternalId,
                        Attachment: prepared.Attachment,
                        OldThumbnailFileIds: prepared.OldThumbnailFileIds));

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
            if (videoTempPath is not null)
            {
                try { File.Delete(videoTempPath); }
                catch (Exception ex)
                {
                    Logger.Warning(
                        ex,
                        "Failed to delete video temp file '{TempPath}'.",
                        videoTempPath);
                }
            }
        }

        return BuildFileResult(file, generatedVariants, failedVariants);
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

    private void ReleaseKey(Guid? keyId)
    {
        if (keyId is { } id)
            keyStore.Remove(id);
    }
}
