using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.Queue;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using PlikShare.Workspaces.Cache;
using Serilog;

namespace PlikShare.MediaProcessing.Dimensions;

public class ExtractImageDimensionsBackfillOperation(
    IClock clock,
    IQueue queue,
    PlikShareDb plikShareDb,
    EphemeralKeyRing ephemeralKeyRing,
    DbWriteQueue dbWriteQueue)
{
    public const int BatchSize = 50;

    private static readonly Serilog.ILogger Logger =
        Log.ForContext<ExtractImageDimensionsBackfillOperation>();

    public async Task Execute(
        WorkspaceContext workspace,
        WorkspaceEncryptionSession? workspaceEncryptionSession,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        var imageFiles = GetWorkspaceImageFiles(
            workspace,
            workspaceEncryptionSession);

        if (imageFiles.Count == 0)
            return;

        var jobs = new List<BulkQueueJobEntity>();

        foreach (var chunk in imageFiles.Chunk(BatchSize))
        {
            var fileIds = new int[chunk.Length];
            Dictionary<int, FullEncryptionSeedEphemeral>? encryptionSeeds = null;

            for (var i = 0; i < chunk.Length; i++)
            {
                var file = chunk[i];

                fileIds[i] = file.Id;

                if (file.EncryptionSeed is not null)
                {
                    encryptionSeeds ??= [];
                    encryptionSeeds[file.Id] = file.EncryptionSeed;
                }
            }
            
            var job = queue.CreateBulkEntity(
                jobType: ExtractImageDimensionsQueueJobType.Value,
                definition: new ExtractImageDimensionsQueueJobDefinition
                {
                    WorkspaceId = workspace.Id,
                    FileIds = fileIds,
                    EncryptionSeeds = encryptionSeeds
                },
                sagaId: null,
                batchId: null);

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

        Logger.Information(
            "Image-dimensions backfill enqueued for Workspace#{WorkspaceId}: {FileCount} files across {JobCount} jobs.",
            workspace.Id,
            imageFiles.Count,
            jobs.Count);
    }

    private List<BackfillImageRow> GetWorkspaceImageFiles(
        WorkspaceContext workspace,
        WorkspaceEncryptionSession? workspaceEncryptionSession)
    {
        using var connection = plikShareDb.OpenConnection();

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
                       AND fi_metadata IS NULL
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

                    var encryptionMetadata = reader.GetByteOrNull(2) is { } keyVersion
                        ? new FileEncryptionMetadata
                        {
                            KeyVersion = keyVersion,
                            Salt = reader.GetFieldValue<byte[]>(3),
                            NoncePrefix = reader.GetFieldValue<byte[]>(4),
                            ChainStepSalts = KeyDerivationChain.Deserialize(
                                reader.GetFieldValueOrNull<byte[]>(5)),
                            FormatVersion = reader.GetByteOrNull(6) ?? 1
                        }
                        : null;

                    acc.Add(new BackfillImageRow(
                        Id: reader.GetInt32(0),
                        EncryptionSeed: workspace.TryGetFileEncryptionSeed(
                            encryptionMetadata, 
                            workspaceEncryptionSession, 
                            ephemeralKeyRing)));

                    return acc;
                })
            .WithParameter("$workspaceId", workspace.Id)
            .Execute();
    }

    private record BackfillImageRow(
        int Id,
        FullEncryptionSeedEphemeral? EncryptionSeed);
}
