using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.Queue;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using PlikShare.Files.Id;
using PlikShare.Files.Metadata;
using PlikShare.Files.PreSignedLinks.Validation;
using PlikShare.Users.Id;
using PlikShare.Workspaces.Cache;

namespace PlikShare.MediaProcessing.Generation;

/// <summary>
/// Enqueues thumbnail generation for many files under a single <c>batchId</c> — one queue job per
/// file (variants inside), inserted in one bulk write. Files that no longer exist or aren't
/// thumbnailable are silently skipped; the returned <c>TotalFiles</c> reflects only the jobs
/// actually enqueued, so the UI's progress denominator matches the real work.
/// </summary>
public class GenerateFileThumbnailsBulkOperation(
    IClock clock,
    IQueue queue,
    DbWriteQueue dbWriteQueue,
    GetFilePreSignedDownloadLinkDetailsQuery getParentFileDetailsQuery,
    TemporaryWorkspaceEncryptionKeyStore keyStore,
    FfmpegService ffmpegService)
{
    public async Task<Result> Execute(
        WorkspaceContext workspace,
        IReadOnlyList<FileExtId> parentFileExternalIds,
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

        var batchId = Guid.NewGuid();
        var variantList = variants.ToList();

        var entities = new List<BulkQueueJobEntity>(parentFileExternalIds.Count);

        // One temp encryption key per job — the worker releases its own key, so a single shared
        // key would be freed by the first finished job and starve the rest of the batch.
        var storedKeyIds = new List<Guid>();

        try
        {
            foreach (var parentFileExternalId in parentFileExternalIds)
            {
                var parentLookup = getParentFileDetailsQuery.Execute(
                    fileExternalId: parentFileExternalId,
                    workspaceEncryptionSession: workspaceEncryptionSession);

                if (parentLookup.Code == GetFilePreSignedDownloadLinkDetailsQuery.ResultCode.NotFound
                    || parentLookup.Details?.WorkspaceId != workspace.Id)
                {
                    continue;
                }

                if (!ContentTypeHelper.IsThumbnailable(parentLookup.Details.Extension))
                    continue;

                var tempKeyId = workspaceEncryptionSession is null
                    ? (Guid?)null
                    : keyStore.Store(workspaceEncryptionSession);

                if (tempKeyId is { } id)
                    storedKeyIds.Add(id);

                var definition = new ProcessImageQueueJobDefinition
                {
                    WorkspaceId = workspace.Id,
                    ParentFileExternalId = parentFileExternalId,
                    Variants = variantList,
                    TempEncryptionKeyId = tempKeyId,
                    TriggeredByUserExternalId = triggeredByUserExternalId
                };

                entities.Add(queue.CreateBulkEntity(
                    jobType: ProcessImageQueueJobType.Value,
                    definition: definition,
                    sagaId: null,
                    batchId: batchId));
            }

            if (entities.Count == 0)
            {
                ReleaseKeys(storedKeyIds);
                return new Result(Code: ResultCode.NoThumbnailableFiles);
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
            // Enqueue failed — release the temp keys immediately rather than leaking until TTL.
            ReleaseKeys(storedKeyIds);
            throw;
        }

        return new Result(
            Code: ResultCode.Ok,
            BatchId: batchId,
            TotalFiles: entities.Count);
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
