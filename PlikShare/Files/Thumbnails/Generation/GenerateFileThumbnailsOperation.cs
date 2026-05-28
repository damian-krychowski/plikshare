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

namespace PlikShare.Files.Thumbnails.Generation;

public class GenerateFileThumbnailsOperation(
    IClock clock,
    IQueue queue,
    DbWriteQueue dbWriteQueue,
    GetFilePreSignedDownloadLinkDetailsQuery getParentFileDetailsQuery,
    TemporaryWorkspaceEncryptionKeyStore keyStore,
    FfmpegService ffmpegService)
{
    public async Task<Result> Execute(
        WorkspaceContext workspace,
        FileExtId parentFileExternalId,
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

        var parentLookup = getParentFileDetailsQuery.Execute(
            fileExternalId: parentFileExternalId,
            workspaceEncryptionSession: workspaceEncryptionSession);

        if (parentLookup.Code == GetFilePreSignedDownloadLinkDetailsQuery.ResultCode.NotFound
            || parentLookup.Details?.WorkspaceId != workspace.Id)
        {
            return new Result(Code: ResultCode.ParentNotFound);
        }

        if (!ContentTypeHelper.IsThumbnailable(parentLookup.Details.Extension))
            return new Result(Code: ResultCode.ParentNotThumbnailable);

        // For full-encrypted workspaces we hand the queue worker a clone of the request-bound
        // session via the in-memory keystore. None/Managed needs no session — the worker
        // decrypts those without it.
        var tempKeyId = workspaceEncryptionSession is null
            ? (Guid?)null
            : keyStore.Store(workspaceEncryptionSession);

        var batchId = Guid.NewGuid();

        var definition = new ProcessImageQueueJobDefinition
        {
            WorkspaceId = workspace.Id,
            ParentFileExternalId = parentFileExternalId,
            Variants = variants.ToList(),
            TempEncryptionKeyId = tempKeyId,
            TriggeredByUserExternalId = triggeredByUserExternalId
        };

        try
        {
            await dbWriteQueue.Execute(
                operationToEnqueue: context => EnqueueJob(
                    dbWriteContext: context,
                    definition: definition,
                    correlationId: correlationId,
                    batchId: batchId),
                cancellationToken: cancellationToken);
        }
        catch
        {
            // Enqueue failed — release the temp key immediately rather than leaking until TTL.
            if (tempKeyId is { } id) {
                keyStore.Remove(id);
            }
            
            throw;
        }

        return new Result(
            Code: ResultCode.Ok, 
            BatchId: batchId);
    }

    private void EnqueueJob(
        SqliteWriteContext dbWriteContext,
        ProcessImageQueueJobDefinition definition,
        Guid correlationId,
        Guid batchId)
    {
        var transaction = dbWriteContext.Connection.BeginTransaction();

        try
        {
            queue.EnqueueOrThrow(
                correlationId: correlationId,
                jobType: ProcessImageQueueJobType.Value,
                definition: definition,
                executeAfterDate: clock.UtcNow,
                debounceId: null,
                sagaId: null,
                dbWriteContext: dbWriteContext,
                transaction: transaction,
                batchId: batchId);

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public record Result(
        ResultCode Code, 
        Guid? BatchId = null);

    public enum ResultCode
    {
        Ok = 0,
        FfmpegUnavailable,
        ParentNotFound,
        ParentNotThumbnailable,
        NoVariants
    }
}
