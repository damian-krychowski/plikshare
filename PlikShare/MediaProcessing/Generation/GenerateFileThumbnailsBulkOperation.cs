using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.Queue;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using PlikShare.Files.Id;
using PlikShare.Files.Metadata;
using PlikShare.Storages.Encryption;
using PlikShare.Users.Id;
using PlikShare.Workspaces.Cache;
using static PlikShare.MediaProcessing.Generation.GetThumbnailableSelectionFilesQuery;

namespace PlikShare.MediaProcessing.Generation;

/// <summary>
/// Enqueues thumbnail generation for many files under a single <c>batchId</c>. Files are grouped
/// into chunks of <see cref="BatchSize"/>; each chunk becomes ONE queue job that processes all its
/// parents together. Lets the executor batch the per-file DB inserts into a single transaction
/// per queue job. Files that no longer exist or aren't thumbnailable are silently skipped.
/// </summary>
public class GenerateFileThumbnailsBulkOperation(
    IClock clock,
    IQueue queue,
    DbWriteQueue dbWriteQueue,
    EphemeralKeyRing ephemeralKeyRing,
    FfmpegService ffmpegService)
{
    // How many parent files to fold into one queue job. Higher = fewer DbWriteQueue trips but
    // larger per-job memory + coarser cancellation/progress granularity.
    public const int BatchSize = 10;

    public async Task<Result> Execute(
        WorkspaceContext workspace,
        List<ThumbnailableFile> thumbnailableFiles,
        IReadOnlyList<ThumbnailVariant> variants,
        UserExtId triggeredByUserExternalId,
        WorkspaceEncryptionSession? workspaceEncryptionSession,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        if (!ffmpegService.IsAvailable)
            return new Result(Code: ResultCode.FfmpegUnavailable);

        if (variants.Count == 0)
            return new Result(Code: ResultCode.NoVariants);

        if (thumbnailableFiles.Count == 0)
            return new Result(Code: ResultCode.NoThumbnailableFiles);

        var batchId = Guid.NewGuid();

        var jobs = new List<BulkQueueJobEntity>();

        foreach (var chunk in thumbnailableFiles.Chunk(BatchSize))
        {
            var imageFileIds = new List<int>();
            var videoFileIds = new List<int>();
            Dictionary<string, FullEncryptionSeedEphemeral>? encryptionSeeds = null;
            
            foreach (var file in chunk)
            {
                if (file.IsVideo())
                {
                    videoFileIds.Add(file.FileId);
                }
                else
                {
                    imageFileIds.Add(file.FileId);
                }

                if (workspace.EncryptionType == StorageEncryptionType.Full)
                {
                    encryptionSeeds ??= [];

                    encryptionSeeds.Add(
                        key: GenerateImageThumbnailsJobDefinition.GetFileEncryptionSeedsKey(
                            fileId: file.FileId),
                        value: FullEncryptionSeedEphemeral.FromFile(
                            fileEncryptionMetadata: file.EncryptionMetadata!,
                            workspace: workspace,
                            session: workspaceEncryptionSession!,
                            ephemeralKeyRing: ephemeralKeyRing));

                    foreach (var variant in variants)
                    {
                        encryptionSeeds.Add(
                            key: GenerateImageThumbnailsJobDefinition.GetVariantEncryptionSeedsKey(
                                fileId: file.FileId,
                                variant: variant),
                            value: FullEncryptionSeedEphemeral.Prepare(
                                workspace: workspace,
                                session: workspaceEncryptionSession!,
                                ephemeralKeyRing: ephemeralKeyRing));
                    }
                }
            }

            var job = queue.CreateBulkEntity(
                jobType: GenerateImageThumbnailsJobType.Value,
                definition: new GenerateImageThumbnailsJobDefinition
                {
                    WorkspaceId = workspace.Id,
                    TriggeredByUserExternalId = triggeredByUserExternalId,
                    Variants = variants.ToArray(),
                    ImageFileIds = imageFileIds,
                    VideoFileIds = videoFileIds,
                    EncryptionSeeds = encryptionSeeds
                },
                sagaId: null,
                batch: new QueueJobBatch(
                    Id: batchId,
                    ItemsCount: chunk.Length));

            jobs.Add(job);
        }

        await dbWriteQueue.Execute(
            operationToEnqueue: context =>
            {
                var transaction = context.Connection.BeginTransaction();

                try
                {
                    queue.EnqueueBulk(
                        correlationId: correlationId,
                        definitions: jobs,
                        executeAfterDate: clock.UtcNow,
                        dbWriteContext: context,
                        transaction: transaction);

                    transaction.Commit();
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            },
            cancellationToken: cancellationToken);

        return new Result(
            Code: ResultCode.Ok,
            BatchId: batchId,
            TotalFiles: thumbnailableFiles.Count);
    }
    
    public record Result(
        ResultCode Code,
        Guid? BatchId = null,
        int TotalFiles = 0);

    public enum ResultCode
    {
        Ok = 0,
        FfmpegUnavailable,
        NoVariants,
        NoThumbnailableFiles
    }
}
