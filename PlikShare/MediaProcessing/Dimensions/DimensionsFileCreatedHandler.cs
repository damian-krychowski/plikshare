using PlikShare.Core.Clock;
using PlikShare.Core.Encryption;
using PlikShare.Core.Queue;
using PlikShare.Files.Created;
using PlikShare.MediaProcessing.Generation;
using PlikShare.Storages.Encryption;
using PlikShare.Workspaces.Cache;
using Serilog;

namespace PlikShare.MediaProcessing.Dimensions;

public class DimensionsFileCreatedHandler(
    IClock clock,
    IQueue queue,
    FfmpegService ffmpegService,
    EphemeralKeyRing ephemeralKeyRing) : IFileCreatedHandler
{
    private const int BatchSize = 20;

    private static readonly Serilog.ILogger Logger =
        Log.ForContext<DimensionsFileCreatedHandler>();

    public void Handle(FileCreatedBatch batch)
    {
        var workspace = batch.Workspace;

        if (workspace.MediaProcessingPolicy?.ExtractImageDimensionsOnUpload != true)
            return;

        if (!ffmpegService.IsAvailable)
            return;

        var isFullEncryption = workspace.EncryptionType == StorageEncryptionType.Full;

        var images = new List<ImageToProcess>();

        foreach (var file in batch.Files)
        {
            if (file.SizeInBytes <= 0)
                continue;

            if (isFullEncryption)
            {
                if (file.EncryptionSeed is not null)
                {
                    // Pre-built when a session was still available (multi-step completion runs in a
                    // queue context without a live session) — its presence means the file was already
                    // vetted as an image at that point.
                    images.Add(new ImageToProcess(file.Id, file.EncryptionSeed));
                }
                else if (batch.Session is not null)
                {
                    var contentType = batch.Session.DecodeMetadata(file.ContentType);

                    if (!contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                        continue;

                    images.Add(new ImageToProcess(
                        file.Id,
                        FullEncryptionSeedEphemeral.FromFile(
                            fileEncryptionMetadata: file.EncryptionMetadata!,
                            workspace: workspace,
                            session: batch.Session,
                            ephemeralKeyRing: ephemeralKeyRing)));
                }
                else
                {
                    Logger.Debug(
                        "Skipping image dimensions for File#{FileId} in full-encryption Workspace#{WorkspaceId} — no session and no pre-built seed (non-image, or accessed without a session); backfill can pick it up if needed.",
                        file.Id,
                        workspace.Id);
                }
            }
            else
            {
                if (!file.ContentType.Encoded.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                    continue;

                images.Add(new ImageToProcess(file.Id, EncryptionSeed: null));
            }
        }

        if (images.Count == 0)
            return;

        foreach (var chunk in images.Chunk(BatchSize))
        {
            var encryptionSeeds = new Dictionary<int, FullEncryptionSeedEphemeral>();

            foreach (var image in chunk)
            {
                if (image.EncryptionSeed is not null)
                    encryptionSeeds[image.Id] = image.EncryptionSeed;
            }

            var job = queue.CreateBulkEntity(
                jobType: ExtractImageDimensionsQueueJobType.Value,
                definition: new ExtractImageDimensionsQueueJobDefinition
                {
                    WorkspaceId = workspace.Id,
                    FileIds = chunk.Select(image => image.Id).ToArray(),
                    EncryptionSeeds = encryptionSeeds
                },
                sagaId: null,
                batch: null);

            queue.EnqueueBulk(
                correlationId: batch.CorrelationId,
                definitions: [job],
                executeAfterDate: clock.UtcNow,
                dbWriteContext: batch.DbWriteContext,
                transaction: batch.Transaction);
        }
    }

    private readonly record struct ImageToProcess(
        int Id,
        FullEncryptionSeedEphemeral? EncryptionSeed);
}
