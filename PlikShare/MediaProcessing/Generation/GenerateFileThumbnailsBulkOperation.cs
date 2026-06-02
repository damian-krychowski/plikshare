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
    GetThumbnailSourceFileQuery getSourceFileQuery,
    TemporaryEncryptionStore temporaryEncryptionStore,
    IMasterDataEncryption masterEncryption,
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

        var entities = new List<BulkQueueJobEntity>();

        // One Package per file — Package holds decryption + encryption + metadata-seed wires
        // (all master-encrypted) for a single parent file's thumbnail generation. Per-file
        // (not per-batch) granularity matches the worker's per-file lifecycle: a Package is
        // taken and consumed for its file then removed.
        var storedHandles = new List<Guid>();

        try
        {
            foreach (var chunk in thumbnailableFiles.Chunk(BatchSize))
            {
                var batchItems = new List<ProcessImageQueueJobDefinition.BatchItem>(
                    chunk.Length);

                foreach (var file in chunk)
                {
                    var handle = ProvisionPackage(
                        workspace: workspace,
                        workspaceEncryptionSession: workspaceEncryptionSession,
                        file: file,
                        variants: variantList);

                    if (handle is { } id)
                        storedHandles.Add(id);

                    batchItems.Add(new ProcessImageQueueJobDefinition.BatchItem
                    {
                        ParentFileExternalId = file.ExternalId,
                        Extension = file.Extension,
                        TempEncryptionKeyId = handle
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
            ReleaseHandles(storedHandles);
            throw;
        }

        return new Result(
            Code: ResultCode.Ok,
            BatchId: batchId,
            TotalFiles: thumbnailableFiles.Count);
    }

    /// <summary>
    /// Per-file: build every wire the worker will need (parent decryption, per-variant
    /// thumbnail encryption, metadata seed), stash them as a Package, return the handle. Null
    /// when the workspace isn't full-encrypted — there's nothing to wrap.
    /// </summary>
    private Guid? ProvisionPackage(
        WorkspaceContext workspace,
        WorkspaceEncryptionSession? workspaceEncryptionSession,
        GetThumbnailSourceFileQuery.ThumbnailSourceFileWithExtensions file,
        List<ThumbnailVariant> variants)
    {
        if (workspaceEncryptionSession is null)
            return null;

        var latest = workspaceEncryptionSession.GetLatestDek();

        // Decryption wire for the parent body — only built when the parent actually has
        // encryption metadata. Worker uses it via FileAesInputsV2.Prepare(wire, masterEnc).
        FileAesInputsV2Wire? decryptionInput = null;
        if (file.EncryptionMetadata is not null)
        {
            decryptionInput = FileAesInputsV2Wire.Prepare(
                ikm: latest.Dek,
                metadata: file.EncryptionMetadata,
                masterEncryption: masterEncryption);
        }

        // Per-variant encryption wires — one per future thumbnail body. Fresh
        // FileEncryptionMetadata generated up-front so the wire carries everything the worker
        // needs to write the body AND populate the new fi_files row's fi_encryption_* columns.
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

        // One seed per file, reused for every attachment metadata value across every variant
        // — each Prepare(seed) call inside the worker generates a fresh per-value metadata
        // salt internally.
        var metadataEncryptionSeed = EncryptionSeedWire.Prepare(
            session: workspaceEncryptionSession,
            masterEncryption: masterEncryption);

        return temporaryEncryptionStore.Store(
            decryptionInput: decryptionInput,
            encryptionInputs: encryptionInputs,
            metadataEncryptionSeed: metadataEncryptionSeed);
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

    private void ReleaseHandles(List<Guid> handles)
    {
        foreach (var handle in handles)
            temporaryEncryptionStore.Remove(handle);
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
