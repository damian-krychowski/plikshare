using Microsoft.Data.Sqlite;
using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.Queue;
using PlikShare.Core.SQLite;
using PlikShare.Core.UserIdentity;
using PlikShare.Core.Utils;
using PlikShare.Files.Metadata;
using PlikShare.Storages.Encryption;
using PlikShare.Workspaces.Cache;
using Serilog;

namespace PlikShare.MediaProcessing.Generation;

/// <summary>
/// Backfills thumbnails for existing workspace images when the thumbnails policy is enabled.
/// Only images MISSING at least one of the requested variants are selected, and each queue job
/// carries only the variants its files are missing — thumbnails that already exist (including
/// manually uploaded ones) are never regenerated or overwritten. All jobs share one batchId so
/// the settings UI can track the whole backfill as a single progress bar.
/// </summary>
public class ThumbnailsBackfillOperation(
    IClock clock,
    IQueue queue,
    PlikShareDb plikShareDb,
    EphemeralKeyRing ephemeralKeyRing,
    DbWriteQueue dbWriteQueue,
    FfmpegService ffmpegService)
{
    public const int BatchSize = 10;

    private static readonly Serilog.ILogger Logger =
        Log.ForContext<ThumbnailsBackfillOperation>();

    public sealed record Result(Guid? BatchId, int TotalFiles);

    public async Task<Result> Execute(
        WorkspaceContext workspace,
        IReadOnlyList<ThumbnailVariant> variants,
        IUserIdentity uploader,
        WorkspaceEncryptionSession? workspaceEncryptionSession,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        if (!ffmpegService.IsAvailable)
            return new Result(BatchId: null, TotalFiles: 0);

        var isFullEncryption = workspace.EncryptionType == StorageEncryptionType.Full;

        if (isFullEncryption && workspaceEncryptionSession is null)
        {
            Logger.Warning(
                "Skipping thumbnails backfill for full-encryption Workspace#{WorkspaceId} — no live encryption session.",
                workspace.Id);

            return new Result(BatchId: null, TotalFiles: 0);
        }

        var imageFiles = GetImagesMissingVariants(
            workspace,
            variants,
            workspaceEncryptionSession);

        if (imageFiles.Count == 0)
            return new Result(BatchId: null, TotalFiles: 0);

        var batchId = Guid.NewGuid();
        var jobs = new List<BulkQueueJobWithTrackedFiles>();

        foreach (var variantsGroup in imageFiles.GroupBy(file => file.MissingVariantsKey))
        {
            var groupVariants = variantsGroup.First().MissingVariants;

            foreach (var chunk in variantsGroup.Chunk(BatchSize))
            {
                Dictionary<string, FullEncryptionSeedEphemeral>? encryptionSeeds = null;

                if (isFullEncryption)
                {
                    encryptionSeeds = [];

                    foreach (var file in chunk)
                    {
                        encryptionSeeds.Add(
                            key: GenerateImageThumbnailsJobDefinition.GetFileEncryptionSeedsKey(
                                fileId: file.Id),
                            value: FullEncryptionSeedEphemeral.FromFile(
                                fileEncryptionMetadata: file.EncryptionMetadata!,
                                workspace: workspace,
                                session: workspaceEncryptionSession!,
                                ephemeralKeyRing: ephemeralKeyRing));

                        foreach (var variant in groupVariants)
                        {
                            encryptionSeeds.Add(
                                key: GenerateImageThumbnailsJobDefinition.GetVariantEncryptionSeedsKey(
                                    fileId: file.Id,
                                    variant: variant),
                                value: FullEncryptionSeedEphemeral.Prepare(
                                    workspace: workspace,
                                    session: workspaceEncryptionSession!,
                                    ephemeralKeyRing: ephemeralKeyRing));
                        }
                    }
                }

                var fileIds = chunk
                    .Select(file => file.Id)
                    .ToArray();

                var job = queue.CreateBulkEntity(
                    jobType: GenerateImageThumbnailsJobType.Value,
                    definition: new GenerateImageThumbnailsJobDefinition
                    {
                        WorkspaceId = workspace.Id,
                        ImageFileIds = [.. fileIds],
                        VideoFileIds = [],
                        Variants = groupVariants,
                        UploaderIdentityType = uploader.IdentityType,
                        UploaderIdentity = uploader.Identity,
                        EncryptionSeeds = encryptionSeeds
                    },
                    sagaId: null,
                    batch: new QueueJobBatch(
                        Id: batchId,
                        ItemsCount: fileIds.Length));

                jobs.Add(new BulkQueueJobWithTrackedFiles(
                    Job: job,
                    TrackedFileIds: fileIds));
            }
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
                        workspaceId: workspace.Id,
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

        Logger.Information(
            "Thumbnails backfill enqueued for Workspace#{WorkspaceId} (Batch {BatchId}): {FileCount} files across {JobCount} jobs.",
            workspace.Id,
            batchId,
            imageFiles.Count,
            jobs.Count);

        return new Result(BatchId: batchId, TotalFiles: imageFiles.Count);
    }

    public int CountImagesToBackfill(
        WorkspaceContext workspace,
        IReadOnlyList<ThumbnailVariant> variants,
        WorkspaceEncryptionSession? workspaceEncryptionSession)
    {
        if (workspace.EncryptionType == StorageEncryptionType.Full && workspaceEncryptionSession is null)
            return 0;

        return GetImagesMissingVariants(
            workspace,
            variants,
            workspaceEncryptionSession).Count;
    }

    private List<BackfillImageRow> GetImagesMissingVariants(
        WorkspaceContext workspace,
        IReadOnlyList<ThumbnailVariant> variants,
        WorkspaceEncryptionSession? workspaceEncryptionSession)
    {
        using var connection = plikShareDb.OpenConnection();

        var imageFiles = GetWorkspaceImageFiles(
            connection,
            workspace,
            workspaceEncryptionSession);

        if (imageFiles.Count == 0)
            return [];

        var existingVariants = GetExistingThumbnailVariants(
            connection,
            workspace,
            workspaceEncryptionSession);

        var result = new List<BackfillImageRow>();

        foreach (var file in imageFiles)
        {
            existingVariants.TryGetValue(file.Id, out var fileVariants);

            var missingVariants = variants
                .Where(variant => fileVariants?.Contains(variant) != true)
                .Distinct()
                .OrderBy(variant => variant)
                .ToArray();

            if (missingVariants.Length == 0)
                continue;

            result.Add(file with
            {
                MissingVariants = missingVariants
            });
        }

        return result;
    }

    private static List<BackfillImageRow> GetWorkspaceImageFiles(
        SqliteConnection connection,
        WorkspaceContext workspace,
        WorkspaceEncryptionSession? workspaceEncryptionSession)
    {
        return connection
            .AggregateRows(
                sql: """
                     SELECT
                         fi_id,
                         fi_content_type,
                         fi_encryption_key_version,
                         fi_encryption_salt,
                         fi_encryption_nonce_prefix,
                         fi_encryption_chain_salts,
                         fi_encryption_format_version
                     FROM fi_files
                     WHERE fi_workspace_id = $workspaceId
                       AND fi_parent_file_id IS NULL
                       AND fi_deleted_at IS NULL
                       AND fi_is_upload_completed = TRUE
                     """,
                seed: new List<BackfillImageRow>(),
                aggregateRowFunc: (acc, reader) =>
                {
                    var contentType = reader.DecodeEncryptableString(
                        ordinal: 1,
                        workspaceEncryptionSession: workspaceEncryptionSession);

                    var fileType = ContentTypeHelper.GetFileTypeFromContentType(
                        contentType);

                    if (fileType != FileType.Image)
                        return acc;

                    acc.Add(new BackfillImageRow(
                        Id: reader.GetInt32(0),
                        EncryptionMetadata: reader.GetByteOrNull(2) is { } keyVersion
                            ? new FileEncryptionMetadata
                            {
                                KeyVersion = keyVersion,
                                Salt = reader.GetFieldValue<byte[]>(3),
                                NoncePrefix = reader.GetFieldValue<byte[]>(4),
                                ChainStepSalts = KeyDerivationChain.Deserialize(
                                    reader.GetFieldValueOrNull<byte[]>(5)),
                                FormatVersion = reader.GetByteOrNull(6) ?? 1
                            }
                            : null,
                        MissingVariants: []));

                    return acc;
                })
            .WithParameter("$workspaceId", workspace.Id)
            .Execute();
    }

    private static Dictionary<int, HashSet<ThumbnailVariant>> GetExistingThumbnailVariants(
        SqliteConnection connection,
        WorkspaceContext workspace,
        WorkspaceEncryptionSession? workspaceEncryptionSession)
    {
        return connection
            .AggregateRows(
                sql: """
                     SELECT
                         fi_parent_file_id,
                         fi_metadata
                     FROM fi_files
                     WHERE fi_workspace_id = $workspaceId
                       AND fi_parent_file_id IS NOT NULL
                       AND fi_deleted_at IS NULL
                       AND fi_is_upload_completed = TRUE
                       AND fi_metadata IS NOT NULL
                     """,
                seed: new Dictionary<int, HashSet<ThumbnailVariant>>(),
                aggregateRowFunc: (acc, reader) =>
                {
                    var metadataJson = reader.DecodeEncryptableBlobOrNull(
                        1,
                        workspaceEncryptionSession);

                    if (metadataJson is null)
                        return acc;

                    var metadata = Json.Deserialize<FileMetadata>(
                        metadataJson);

                    if (metadata is not ThumbnailFileMetadata thumbnailMetadata)
                        return acc;

                    var parentFileId = reader.GetInt32(0);

                    if (!acc.TryGetValue(parentFileId, out var fileVariants))
                    {
                        fileVariants = [];
                        acc[parentFileId] = fileVariants;
                    }

                    fileVariants.Add(thumbnailMetadata.Variant);

                    return acc;
                })
            .WithParameter("$workspaceId", workspace.Id)
            .Execute();
    }

    private sealed record BackfillImageRow(
        int Id,
        FileEncryptionMetadata? EncryptionMetadata,
        ThumbnailVariant[] MissingVariants)
    {
        public string MissingVariantsKey => string.Join(
            ",",
            MissingVariants);
    }
}
