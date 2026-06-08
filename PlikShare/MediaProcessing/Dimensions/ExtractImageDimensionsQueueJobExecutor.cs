using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.Queue;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using PlikShare.Files.Id;
using PlikShare.Files.Metadata;
using PlikShare.Files.PreSignedLinks.RangeRequests;
using PlikShare.MediaProcessing.Generation;
using PlikShare.Storages;
using PlikShare.Storages.Encryption;
using PlikShare.Workspaces.Cache;
using Serilog;

namespace PlikShare.MediaProcessing.Dimensions;

public class ExtractImageDimensionsQueueJobExecutor(
    PlikShareDb plikShareDb,
    WorkspaceCache workspaceCache,
    FfmpegService ffmpegService,
    EphemeralKeyRing ephemeralKeyRing) : IQueueLongRunningJobExecutor
{
    private const long HeaderRangeLimit = 1L * 1024 * 1024;

    private static readonly Serilog.ILogger Logger =
        Log.ForContext<ExtractImageDimensionsQueueJobExecutor>();

    public static string StaticJobType => ExtractImageDimensionsQueueJobType.Value;
    public static int StaticPriority => QueueJobPriority.ExtremelyLow;

    public string JobType => StaticJobType;
    public int Priority => StaticPriority;

    public async Task<QueueJobResult> Execute(
        string definitionJson,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        var definition = Json.Deserialize<ExtractImageDimensionsQueueJobDefinition>(definitionJson)
            ?? throw new ArgumentException(
                $"Could not deserialize job definition: {definitionJson}");

        if (!ffmpegService.IsAvailable)
        {
            Logger.Warning(
                "Skipping image dimensions extraction for batch of {Count} files — ffmpeg is not available. " +
                "Scheduling soft retry in case it gets installed.",
                definition.FileIds.Length);

            return QueueJobResult.NeedsRetry(
                maxAttempts: 3,
                delay: TimeSpan.FromMinutes(10));
        }

        var workspace = await workspaceCache.TryGetWorkspace(
            workspaceId: definition.WorkspaceId,
            cancellationToken: cancellationToken);

        if (workspace is null)
        {
            Logger.Warning(
                "Workspace#{WorkspaceId} not found — dropping image dimensions extraction job for batch of {Count} files.",
                definition.WorkspaceId,
                definition.FileIds.Length);

            return QueueJobResult.Success;
        }

        if (workspace.MediaProcessingPolicy?.ExtractImageDimensionsOnUpload != true)
        {
            Logger.Information(
                "Workspace#{WorkspaceId} extract-image-dimensions policy is disabled — skipping batch of {Count} files.",
                workspace.Id,
                definition.FileIds.Length);

            return QueueJobResult.Success;
        }

        var isFullEncryption = workspace.EncryptionType == StorageEncryptionType.Full;

        var imageFiles = GetImageFiles(
            workspace,
            definition.FileIds);

        var updates = new List<UpsertParentImageDimensionsQuery.DimensionsUpdate>();

        foreach (var imageFile in imageFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (imageFile.HasImageDimensions || imageFile.SizeInBytes <= 0)
                continue;

            FullEncryptionSeed? seed = null;

            try
            {
                if (isFullEncryption)
                {
                    if (!definition.EncryptionSeeds.TryGetValue(imageFile.Id, out var ephemeral))
                    {
                        Logger.Warning(
                            "Full-encryption File '{FileExternalId}' in Workspace#{WorkspaceId} has no ephemeral encryption seed in the job definition — skipping.",
                            imageFile.ExternalId,
                            workspace.Id);

                        continue;
                    }

                    var status = ephemeral.TryDecode(ephemeralKeyRing, out seed);

                    if (status != EphemeralDecodeStatus.Ok || seed is null)
                    {
                        Logger.Warning(
                            "Ephemeral encryption key could not be decoded ({Status}) for File '{FileExternalId}' in Workspace#{WorkspaceId} — skipping.",
                            status,
                            imageFile.ExternalId,
                            workspace.Id);

                        continue;
                    }
                }

                using (seed)
                {
                    var dimensions = await TryExtractDimensions(
                        workspace,
                        imageFile,
                        seed,
                        cancellationToken);

                    if (dimensions is null)
                    {
                        Logger.Information(
                            "Could not extract dimensions for File '{FileExternalId}' in Workspace#{WorkspaceId}.",
                            imageFile.ExternalId,
                            workspace.Id);

                        continue;
                    }

                    updates.Add(new UpsertParentImageDimensionsQuery.DimensionsUpdate(
                        FileExternalId: imageFile.ExternalId,
                        EncodedMetadata: EncodeDimensions(
                            seed,
                            dimensions.Value.Width,
                            dimensions.Value.Height)));
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.Warning(
                    ex,
                    "Image dimensions extraction failed for File '{FileExternalId}' in Workspace#{WorkspaceId}.",
                    imageFile.ExternalId,
                    workspace.Id);
            }
        }

        if (updates.Count == 0)
            return QueueJobResult.Success;

        return QueueJobResult.SuccessWithDbWrite(
            dbWrite: (dbWriteContext, transaction) => UpsertParentImageDimensionsQuery.WriteBatch(
                dbWriteContext: dbWriteContext,
                transaction: transaction,
                workspace: workspace,
                items: updates));
    }

    private static EncodedMetadataValue EncodeDimensions(
        FullEncryptionSeed? seed,
        int width,
        int height)
    {
        var json = Json.Serialize<FileMetadata>(
            new ImageDimensionsFileMetadata
            {
                Width = width,
                Height = height
            });

        return seed is null
            ? NoMetadataEncryptionSeed.Instance.EncodeMetadata(json)
            : seed.DeriveNew().EncodeMetadata(json);
    }

    private async Task<(int Width, int Height)?> TryExtractDimensions(
        WorkspaceContext workspace,
        ImageFileRow imageFile,
        FullEncryptionSeed? seed,
        CancellationToken cancellationToken)
    {
        var encryptionMode = seed is null
            ? workspace.GetFileEncryptionMode(
                fileEncryptionMetadata: imageFile.EncryptionMetadata,
                workspaceEncryptionSession: null)
            : seed.ToFileEncryptionMode(
                imageFile.EncryptionMetadata!);

        var rangeEnd = Math.Min(
            HeaderRangeLimit - 1,
            imageFile.SizeInBytes - 1);

        await using var storageFile = await workspace.Storage.DownloadFileRange(
            fileDetails: new DownloadFileRangeDetails(
                Range: new BytesRange(
                    Start: 0,
                    End: rangeEnd),
                FileKey: new FileKey
                {
                    FileExternalId = imageFile.ExternalId,
                    KeySecretPart = imageFile.KeySecretPart
                },
                FileSizeInBytes: imageFile.SizeInBytes,
                EncryptionMode: encryptionMode),
            bucketName: workspace.BucketName,
            cancellationToken: cancellationToken);

        return await ffmpegService.ProbeImageDimensions(
            writeSourceTo: storageFile.ReadTo,
            cancellationToken: cancellationToken);
    }

    private List<ImageFileRow> GetImageFiles(
        WorkspaceContext workspace,
        int[] fileIds)
    {
        if (fileIds.Length == 0)
            return [];

        using var connection = plikShareDb.OpenConnection();

        return connection
            .AggregateRows(
                sql: """
                     SELECT
                         fi_id,
                         fi_external_id,
                         fi_key_secret_part,
                         fi_size_in_bytes,
                         fi_encryption_key_version,
                         fi_encryption_salt,
                         fi_encryption_nonce_prefix,
                         fi_encryption_chain_salts,
                         fi_encryption_format_version,
                         fi_metadata
                     FROM fi_files
                     WHERE fi_workspace_id = $workspaceId
                       AND fi_parent_file_id IS NULL
                       AND fi_deleted_at IS NULL
                       AND fi_is_upload_completed = TRUE
                       AND fi_id IN (
                           SELECT value FROM json_each($fileIds)
                       )
                     """,
                seed: new List<ImageFileRow>(),
                aggregateRowFunc: (rows, reader) =>
                {
                    var isFullEncryption = workspace.EncryptionType == StorageEncryptionType.Full;

                    var encryptionMetadata = reader.GetByteOrNull(4) is { } keyVersion
                        ? new FileEncryptionMetadata
                        {
                            KeyVersion = keyVersion,
                            Salt = reader.GetFieldValue<byte[]>(5),
                            NoncePrefix = reader.GetFieldValue<byte[]>(6),
                            ChainStepSalts = KeyDerivationChain.Deserialize(
                                reader.GetFieldValueOrNull<byte[]>(7)),
                            FormatVersion = reader.GetByteOrNull(8) ?? 1
                        }
                        : null;

                    var hasImageDimensions = !isFullEncryption
                        && ImageDimensionsMetadata.Read(
                            reader,
                            ordinal: 9,
                            workspaceEncryptionSession: null) is not null;

                    rows.Add(new ImageFileRow
                    {
                        Id = reader.GetInt32(0),
                        ExternalId = reader.GetExtId<FileExtId>(1),
                        KeySecretPart = reader.GetString(2),
                        SizeInBytes = reader.GetInt64(3),
                        EncryptionMetadata = encryptionMetadata,
                        HasImageDimensions = hasImageDimensions
                    });

                    return rows;
                })
            .WithParameter("$workspaceId", workspace.Id)
            .WithJsonParameter("$fileIds", fileIds)
            .Execute();
    }

    private sealed class ImageFileRow
    {
        public required int Id { get; init; }
        public required FileExtId ExternalId { get; init; }
        public required string KeySecretPart { get; init; }
        public required long SizeInBytes { get; init; }
        public required FileEncryptionMetadata? EncryptionMetadata { get; init; }
        public required bool HasImageDimensions { get; init; }
    }
}
