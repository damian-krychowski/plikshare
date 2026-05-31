using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.Queue;
using PlikShare.Core.SQLite;
using PlikShare.Files.Id;
using PlikShare.Files.Metadata;
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
    GetThumbnailableFilesQuery getThumbnailableFilesQuery,
    TemporaryWorkspaceEncryptionKeyStore keyStore,
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

        var thumbnailableFiles = getThumbnailableFilesQuery.Execute(
            workspace: workspace,
            fileExternalIds: parentFileExternalIds,
            workspaceEncryptionSession: workspaceEncryptionSession);

        if (thumbnailableFiles.Count == 0)
            return new Result(Code: ResultCode.NoThumbnailableFiles);

        var batchId = Guid.NewGuid();
        var variantList = variants.ToList();

        var entities = new List<BulkQueueJobEntity>();

        // One temp encryption key PER FILE — the worker releases per-key, so a single shared key
        // would be freed by the first finished file and starve the rest.
        var storedKeyIds = new List<Guid>();

        try
        {
            foreach (var chunk in thumbnailableFiles.Chunk(BatchSize))
            {
                var batchItems = new List<ProcessImageQueueJobDefinition.BatchItem>(chunk.Length);

                foreach (var file in chunk)
                {
                    var tempKeyId = workspaceEncryptionSession is null
                        ? (Guid?)null
                        : keyStore.Store(workspaceEncryptionSession);

                    if (tempKeyId is { } id)
                        storedKeyIds.Add(id);

                    batchItems.Add(new ProcessImageQueueJobDefinition.BatchItem
                    {
                        ParentFileExternalId = file.ExternalId,
                        Extension = file.Extension,
                        TempEncryptionKeyId = tempKeyId
                    });
                }

                var definition = new ProcessImageQueueJobDefinition
                {
                    WorkspaceId = workspace.Id,
                    Files = batchItems,
                    Variants = variantList,
                    TriggeredByUserExternalId = triggeredByUserExternalId
                };

                entities.Add(queue.CreateBulkEntity(
                    jobType: ProcessImageQueueJobType.Value,
                    definition: definition,
                    sagaId: null,
                    batchId: batchId));
            }

            await dbWriteQueue.Execute(
                operationToEnqueue: context => EnqueueJobs(
                    dbWriteContext: context,
                    entities: entities,
                    correlationId: correlationId),
                cancellationToken: cancellationToken);
        }
        catch
        {
            ReleaseKeys(storedKeyIds);
            throw;
        }

        return new Result(
            Code: ResultCode.Ok,
            BatchId: batchId,
            TotalFiles: thumbnailableFiles.Count);
    }

    private void EnqueueJobs(
        SqliteWriteContext dbWriteContext,
        List<BulkQueueJobEntity> entities,
        Guid correlationId)
    {
        var transaction = dbWriteContext.Connection.BeginTransaction();

        try
        {
            queue.EnqueueBulk(
                correlationId: correlationId,
                definitions: entities,
                executeAfterDate: clock.UtcNow,
                dbWriteContext: dbWriteContext,
                transaction: transaction);

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private void ReleaseKeys(List<Guid> keyIds)
    {
        foreach (var keyId in keyIds)
            keyStore.Remove(keyId);
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
