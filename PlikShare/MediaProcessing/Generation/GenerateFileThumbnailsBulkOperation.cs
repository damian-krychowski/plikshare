using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.Queue;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using PlikShare.Files.Metadata;
using PlikShare.Storages.Encryption;
using PlikShare.Users.Id;
using PlikShare.Workspaces.Cache;

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
    GetThumbnailSourceFileQuery getSourceFileQuery,
    EphemeralKeyRing ephemeralKeyRing,
    FfmpegService ffmpegService)
{
    // How many parent files to fold into one queue job. Higher = fewer DbWriteQueue trips but
    // larger per-job memory + coarser cancellation/progress granularity.
    public const int BatchSize = 10;

    public async Task<Result> Execute(
        WorkspaceContext workspace,
        List<string> parentFileExternalIds,
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

        if (parentFileExternalIds.Count == 0)
            return new Result(Code: ResultCode.NoThumbnailableFiles);

        var thumbnailableFiles = getSourceFileQuery.ExecuteBatch(
            parentFileExternalIds,
            workspaceEncryptionSession);
        
        if (thumbnailableFiles.Count == 0)
            return new Result(Code: ResultCode.NoThumbnailableFiles);
        
        var batchId = Guid.NewGuid();
        var variantList = variants.ToList();
        
        var allBatchItems = new List<ProcessImageQueueJobDefinitionV2.BatchItem>();

        foreach (var parentFile in thumbnailableFiles)
        {
            var variantItems = new List<ProcessImageQueueJobDefinitionV2.VariantItem>();

            foreach (var variant in variantList)
            {
                variantItems.Add(new ProcessImageQueueJobDefinitionV2.VariantItem
                {
                    Variant = variant,

                    EncryptionSeed = workspace.EncryptionType == StorageEncryptionType.Full
                        ? FullEncryptionSeedEphemeral.Prepare(
                            workspace: workspace,
                            session: workspaceEncryptionSession!,
                            ephemeralKeyRing: ephemeralKeyRing)
                        : null
                });
            }

            allBatchItems.Add(new ProcessImageQueueJobDefinitionV2.BatchItem
            {
                ParentFileExternalId = parentFile.ExternalId,
                VariantItems = variantItems,
                IsVideo = ContentTypeHelper.GetFileTypeFromExtension(parentFile.Extension) == FileType.Video,

                EncryptionSeed = workspace.EncryptionType == StorageEncryptionType.Full
                    ? FullEncryptionSeedEphemeral.FromFile(
                        fileEncryptionMetadata: parentFile.EncryptionMetadata!,
                        workspace: workspace,
                        session: workspaceEncryptionSession!,
                        ephemeralKeyRing: ephemeralKeyRing)
                    : null
            });
        }

        var jobs = new List<BulkQueueJobEntity>();

        foreach (var chunk in allBatchItems.Chunk(BatchSize))
        {
            var job = queue.CreateBulkEntity(
                jobType: ProcessImageQueueJobTypeV2.Value,
                definition: new ProcessImageQueueJobDefinitionV2
                {
                    WorkspaceId = workspace.Id,
                    Files = chunk,
                    TriggeredByUserExternalId = triggeredByUserExternalId
                },
                sagaId: null,
                batchId: batchId);

            jobs.Add(job);
        }

        await dbWriteQueue.Execute(
            operationToEnqueue: context =>
            {
                context.Connection.RegisterJsonArrayToBlobFunction();
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
