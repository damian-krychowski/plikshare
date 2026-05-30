using PlikShare.Core.Encryption;
using PlikShare.Core.Queue;
using PlikShare.Core.UserIdentity;
using PlikShare.Core.Utils;
using PlikShare.Files.Id;
using PlikShare.Files.PreSignedLinks.Validation;
using PlikShare.Files.Records;
using PlikShare.Storages;
using PlikShare.Workspaces.Cache;
using Serilog;

namespace PlikShare.Files.Thumbnails.Generation;

/// <summary>
/// Background worker that takes a parent file + variant list, decrypts the original through
/// the (optional) ephemeral workspace encryption session resolved from the temporary keystore,
/// pipes the bytes through ffmpeg once per variant, and uploads each generated WebP via
/// <see cref="UploadFileThumbnailOperation"/> — which handles the same replace-old + atomic
/// finalize semantics as manual uploads.
///
/// Failure handling:
/// <list type="bullet">
///   <item>Workspace gone, parent gone, or temp key gone → log + Success (drop the job).</item>
///   <item>ffmpeg unavailable → soft retry (the binary may have been installed since the job was enqueued).</item>
///   <item>Single variant fails ffmpeg → log + continue with remaining variants.</item>
///   <item>Upload fails → bubbles up as job failure (will hit standard queue retry).</item>
/// </list>
/// </summary>
public class ProcessImageQueueJobExecutor(
    WorkspaceCache workspaceCache,
    GetFilePreSignedDownloadLinkDetailsQuery getFileDetailsQuery,
    TemporaryWorkspaceEncryptionKeyStore keyStore,
    UploadFileThumbnailOperation uploadFileThumbnailOperation,
    FfmpegService ffmpegService) : IQueueLongRunningJobExecutor
{
    private static readonly Serilog.ILogger Logger = Log.ForContext<ProcessImageQueueJobExecutor>();

    public string JobType => ProcessImageQueueJobType.Value;
    public int Priority => QueueJobPriority.Normal;

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
                "Skipping image processing for File '{ParentFileExternalId}' — ffmpeg is not available. " +
                "Scheduling soft retry in case it gets installed.",
                definition.ParentFileExternalId);

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
                "Workspace#{WorkspaceId} not found — dropping image processing job for File '{ParentFileExternalId}'.",
                definition.WorkspaceId,
                definition.ParentFileExternalId);

            ReleaseKey(definition.TempEncryptionKeyId);
            return QueueJobResult.Success;
        }

        WorkspaceEncryptionSession? session = null;

        if (definition.TempEncryptionKeyId is { } keyId)
        {
            session = keyStore.TryRetrieve(keyId);

            if (session is null)
            {
                Logger.Warning(
                    "Temporary workspace encryption key {KeyId} not found — process likely restarted " +
                    "or TTL elapsed before the job ran. Dropping job for File '{ParentFileExternalId}'; " +
                    "user must retrigger.",
                    keyId,
                    definition.ParentFileExternalId);

                return QueueJobResult.Success;
            }
        }

        var parentLookup = getFileDetailsQuery.Execute(
            fileExternalId: definition.ParentFileExternalId,
            workspaceEncryptionSession: session);

        if (parentLookup.Code == GetFilePreSignedDownloadLinkDetailsQuery.ResultCode.NotFound
            || parentLookup.Details?.WorkspaceId != workspace.Id)
        {
            Logger.Warning(
                "Parent File '{ParentFileExternalId}' not found in Workspace#{WorkspaceId} — dropping.",
                definition.ParentFileExternalId,
                workspace.Id);

            ReleaseKey(definition.TempEncryptionKeyId);
            return QueueJobResult.Success;
        }

        var parent = parentLookup.Details;

        var encryptionMode = parent.EncryptionMetadata.ToEncryptionMode(
            workspaceEncryptionSession: session,
            storageClient: workspace.Storage);

        var uploader = new UserIdentity(
            UserExternalId: definition.TriggeredByUserExternalId);

        // A per-variant ffmpeg/upload failure is collected, not thrown — one bad image must not
        // fail the queue job (and, for bulk, must not block the rest of the batch). The collected
        // failures are returned as the job's result payload so the user can see what didn't work.
        var failedVariants = new List<ThumbnailGenerationResult.FailedVariant>();
        var generatedVariants = new List<ThumbnailGenerationResult.GeneratedVariant>();

        try
        {
            await using var storageFile = await workspace.Storage.DownloadFile(
                fileDetails: new DownloadFileDetails(
                    FileKey: new FileKey
                    {
                        FileExternalId = parent.ExternalId,
                        KeySecretPart = parent.KeySecretPart
                    },
                    FileSizeInBytes: parent.SizeInBytes,
                    EncryptionMode: encryptionMode),
                bucketName: workspace.BucketName,
                cancellationToken: cancellationToken);

            // Stream the source ONCE from storage and fan it out to one ffmpeg process per variant
            // over stdin — the original is never buffered in memory or on disk.
            var outputs = await ffmpegService.GenerateThumbnails(
                writeSourceTo: (writer, ct) => storageFile.ReadTo(writer, ct),
                variants: definition.Variants,
                cancellationToken: cancellationToken);

            foreach (var output in outputs)
            {
                if (output.Error is not null)
                {
                    Logger.Error(
                        "Failed to generate {Variant} thumbnail for File '{ParentFileExternalId}': {Error}",
                        output.Variant,
                        definition.ParentFileExternalId,
                        output.Error);

                    failedVariants.Add(new ThumbnailGenerationResult.FailedVariant
                    {
                        Variant = output.Variant,
                        Error = Truncate(output.Error, maxLength: 500)
                    });

                    continue;
                }

                try
                {
                    await using var thumbStream = output.DataStream!;

                    var uploadResult = await uploadFileThumbnailOperation.Execute(
                        workspace: workspace,
                        parentFileExternalId: definition.ParentFileExternalId,
                        thumbnailFileExternalId: FileExtId.NewId(),
                        variant: output.Variant,
                        thumbnailContent: thumbStream,
                        thumbnailSizeInBytes: output.DataStream!.Length,
                        thumbnailContentType: "image/webp",
                        thumbnailFileName: $"thumb-{output.Variant.ToString().ToLowerInvariant()}",
                        thumbnailFileExtension: ".webp",
                        uploader: uploader,
                        workspaceEncryptionSession: session,
                        correlationId: correlationId,
                        cancellationToken: cancellationToken);

                    if (uploadResult.Code != UploadFileThumbnailOperation.ResultCode.Ok)
                    {
                        Logger.Warning(
                            "Upload of generated {Variant} thumbnail returned {Code} for File '{ParentFileExternalId}'.",
                            output.Variant,
                            uploadResult.Code,
                            definition.ParentFileExternalId);

                        failedVariants.Add(new ThumbnailGenerationResult.FailedVariant
                        {
                            Variant = output.Variant,
                            Error = $"Upload failed: {uploadResult.Code}"
                        });
                    }
                    else
                    {
                        generatedVariants.Add(new ThumbnailGenerationResult.GeneratedVariant
                        {
                            Variant = output.Variant,
                            Etag = uploadResult.Etag!
                        });
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(
                        ex,
                        "Failed to upload {Variant} thumbnail for File '{ParentFileExternalId}'.",
                        output.Variant,
                        definition.ParentFileExternalId);

                    failedVariants.Add(new ThumbnailGenerationResult.FailedVariant
                    {
                        Variant = output.Variant,
                        Error = Truncate(ex.Message, maxLength: 500)
                    });
                }
            }
        }
        finally
        {
            ReleaseKey(definition.TempEncryptionKeyId);
        }

        return QueueJobResult.SuccessWithResult(
            Json.Serialize(new ThumbnailGenerationResult
            {
                ParentFileExternalId = definition.ParentFileExternalId,
                GeneratedVariants = generatedVariants,
                FailedVariants = failedVariants
            }));
    }

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
