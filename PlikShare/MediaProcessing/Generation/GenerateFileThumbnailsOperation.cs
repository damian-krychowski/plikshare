using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.Queue;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using PlikShare.Files.Id;
using PlikShare.Files.Metadata;
using PlikShare.Files.PreSignedLinks.Validation;
using PlikShare.Storages;
using PlikShare.Users.Id;
using PlikShare.Workspaces.Cache;

namespace PlikShare.MediaProcessing.Generation;

/// <summary>
/// Enqueues thumbnail generation for one parent file. Wraps the file in a one-element
/// <see cref="ProcessImageQueueJobDefinition"/> batch so the worker handles single-file and bulk
/// uniformly.
/// </summary>
public class GenerateFileThumbnailsOperation(
    IClock clock,
    IQueue queue,
    DbWriteQueue dbWriteQueue,
    GetFilePreSignedDownloadLinkDetailsQuery getParentFileDetailsQuery,
    TemporaryEncryptionStore temporaryEncryptionStore,
    IMasterDataEncryption masterEncryption,
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

        // Pre-derive every wire the worker will need for this one file: parent decryption,
        // per-variant thumbnail encryption, metadata seed. Wrapped in one Package; the queue
        // payload carries only the handle. Null for None/Managed workspaces (no encryption).
        var handle = ProvisionPackage(
            workspace: workspace,
            workspaceEncryptionSession: workspaceEncryptionSession,
            parentEncryptionMetadata: parentLookup.Details.EncryptionMetadata,
            variants: variants);

        var batchId = Guid.NewGuid();

        var definition = new ProcessImageQueueJobDefinition
        {
            WorkspaceId = workspace.Id,
            Files =
            [
                new ProcessImageQueueJobDefinition.BatchItem
                {
                    ParentFileExternalId = parentFileExternalId,
                    Extension = parentLookup.Details.Extension,
                    TempEncryptionKeyId = handle
                }
            ],
            Variants = variants.ToList(),
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
            if (handle is { } id)
                temporaryEncryptionStore.Remove(id);

            throw;
        }

        return new Result(
            Code: ResultCode.Ok,
            BatchId: batchId);
    }

    private Guid? ProvisionPackage(
        WorkspaceContext workspace,
        WorkspaceEncryptionSession? workspaceEncryptionSession,
        FileEncryptionMetadata? parentEncryptionMetadata,
        IReadOnlyList<ThumbnailVariant> variants)
    {
        if (workspaceEncryptionSession is null)
            return null;

        var latest = workspaceEncryptionSession.GetLatestDek();

        var decryptionInput = parentEncryptionMetadata is null
            ? null
            : FileAesInputsV2Wire.Prepare(
                ikm: latest.Dek,
                metadata: parentEncryptionMetadata,
                masterEncryption: masterEncryption);

        var encryptionInputs = new List<FileAesInputsV2Wire>(variants.Count);

        foreach (var _ in variants)
        {
            var newMetadata = workspace.GenerateFileEncryptionMetadata();

            if (newMetadata is null)
                continue;

            encryptionInputs.Add(FileAesInputsV2Wire.Prepare(
                ikm: latest.Dek,
                metadata: newMetadata,
                masterEncryption: masterEncryption));
        }

        var metadataEncryptionSeed = EncryptionSeedWire.Prepare(
            session: workspaceEncryptionSession,
            masterEncryption: masterEncryption);

        return temporaryEncryptionStore.Store(
            decryptionInput: decryptionInput,
            encryptionInputs: encryptionInputs,
            metadataEncryptionSeed: metadataEncryptionSeed);
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
