using PlikShare.Core.Clock;
using PlikShare.Core.Encryption;
using PlikShare.Core.Queue;
using PlikShare.Files.Created;
using PlikShare.Storages.Encryption;
using PlikShare.Workspaces.Cache;
using Serilog;

namespace PlikShare.MediaProcessing.Generation;

public class ThumbnailsFileCreatedHandler(
    IClock clock,
    IQueue queue,
    FfmpegService ffmpegService,
    EphemeralKeyRing ephemeralKeyRing) : IFileCreatedHandler
{
    private const int BatchSize = 10;

    private static readonly Serilog.ILogger Logger =
        Log.ForContext<ThumbnailsFileCreatedHandler>();

    public void Handle(FileCreatedBatch batch)
    {
        var workspace = batch.Workspace;

        if (workspace.MediaProcessingPolicy?.GenerateThumbnailsOnUpload != true)
            return;

        if (!ffmpegService.IsAvailable)
            return;

        var isFullEncryption = workspace.EncryptionType == StorageEncryptionType.Full;

        if (isFullEncryption && batch.Session is null)
        {
            // Thumbnails are new encrypted files, so each variant needs a fresh encryption seed
            // derived from the workspace DEK — and that requires a live session. Multi-step upload
            // completion runs in a queue context without one; manual generation can cover those files.
            Logger.Debug(
                "Skipping on-upload thumbnails for {Count} file(s) in full-encryption Workspace#{WorkspaceId} — no live encryption session to derive thumbnail keys from.",
                batch.Files.Count,
                workspace.Id);

            return;
        }

        var variants = workspace
            .MediaProcessingPolicy
            .Thumbnails!
            .Variants;

        var images = new List<ImageToProcess>();

        foreach (var file in batch.Files)
        {
            if (file.SizeInBytes <= 0)
                continue;

            if (isFullEncryption)
            {
                var contentType = batch.Session!.DecodeMetadata(file.ContentType);

                if (!contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                    continue;

                images.Add(new ImageToProcess(
                    Id: file.Id,
                    UploaderIdentityType: file.UploaderIdentityType,
                    UploaderIdentity: file.UploaderIdentity,
                    EncryptionSeed: file.EncryptionSeed ?? FullEncryptionSeedEphemeral.FromFile(
                        fileEncryptionMetadata: file.EncryptionMetadata!,
                        workspace: workspace,
                        session: batch.Session!,
                        ephemeralKeyRing: ephemeralKeyRing)));
            }
            else
            {
                if (!file.ContentType.Encoded.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                    continue;

                images.Add(new ImageToProcess(
                    Id: file.Id,
                    UploaderIdentityType: file.UploaderIdentityType,
                    UploaderIdentity: file.UploaderIdentity,
                    EncryptionSeed: null));
            }
        }

        if (images.Count == 0)
            return;

        foreach (var uploaderGroup in images.GroupBy(image => (image.UploaderIdentityType, image.UploaderIdentity)))
        {
            foreach (var chunk in uploaderGroup.Chunk(BatchSize))
            {
                Dictionary<string, FullEncryptionSeedEphemeral>? encryptionSeeds = null;

                if (isFullEncryption)
                {
                    encryptionSeeds = [];

                    foreach (var image in chunk)
                    {
                        encryptionSeeds.Add(
                            key: GenerateImageThumbnailsJobDefinition.GetFileEncryptionSeedsKey(
                                fileId: image.Id),
                            value: image.EncryptionSeed!);

                        foreach (var variant in variants)
                        {
                            encryptionSeeds.Add(
                                key: GenerateImageThumbnailsJobDefinition.GetVariantEncryptionSeedsKey(
                                    fileId: image.Id,
                                    variant: variant),
                                value: FullEncryptionSeedEphemeral.Prepare(
                                    workspace: workspace,
                                    session: batch.Session!,
                                    ephemeralKeyRing: ephemeralKeyRing));
                        }
                    }
                }

                var fileIds = chunk
                    .Select(image => image.Id)
                    .ToArray();

                var job = queue.CreateBulkEntity(
                    jobType: GenerateImageThumbnailsJobType.Value,
                    definition: new GenerateImageThumbnailsJobDefinition
                    {
                        WorkspaceId = workspace.Id,
                        ImageFileIds = [.. fileIds],
                        VideoFileIds = [],
                        Variants = variants,
                        UploaderIdentityType = uploaderGroup.Key.UploaderIdentityType,
                        UploaderIdentity = uploaderGroup.Key.UploaderIdentity,
                        EncryptionSeeds = encryptionSeeds
                    },
                    sagaId: null,
                    batch: null);

                queue.EnqueueBulk(
                    correlationId: batch.CorrelationId,
                    definitions: [new BulkQueueJobWithTrackedFiles(
                        Job: job,
                        TrackedFileIds: fileIds)],
                    executeAfterDate: clock.UtcNow,
                    workspaceId: workspace.Id,
                    dbWriteContext: batch.DbWriteContext,
                    transaction: batch.Transaction);
            }
        }
    }

    private readonly record struct ImageToProcess(
        int Id,
        string UploaderIdentityType,
        string UploaderIdentity,
        FullEncryptionSeedEphemeral? EncryptionSeed);
}
