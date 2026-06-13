using Microsoft.Data.Sqlite;
using PlikShare.AuditLog.Id;
using PlikShare.Core.Clock;
using PlikShare.Core.Database.AuditLogDatabase;
using PlikShare.Core.SQLite;
using PlikShare.GeneralSettings;
using Serilog;

namespace PlikShare.AuditLog;

public class AuditLogWriter(
    AuditLogChannel channel,
    PlikShareAuditLogDb plikShareAuditLogDb,
    AppSettings appSettings,
    IClock clock) : BackgroundService
{
    private static readonly Serilog.ILogger Logger = Log.ForContext<AuditLogWriter>();

    private const int CheckpointAfterEntries = 1000;
    private static readonly TimeSpan CheckpointInterval = TimeSpan.FromSeconds(30);

    private const double MaxSizeLowWaterFraction = 0.9;
    private const int MaxSizeDeleteBatch = 5000;
    private const int MaxSizeMaxBatchesPerCycle = 20;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Logger.Information("AuditLogWriter started.");

        using var connection = plikShareAuditLogDb.OpenConnection();
        using var commandsPool = connection.CreateLazyCommandsPool();

        var context = new SqliteWriteContext
        {
            Connection = connection,
            CommandsPool = commandsPool
        };

        var entriesSinceCheckpoint = 0;
        var lastCheckpointAt = clock.UtcNow;

        try
        {
            while (await channel.Reader.WaitToReadAsync(stoppingToken))
            {
                while (channel.Reader.TryRead(out var entry))
                {
                    try
                    {
                        WriteEntry(context, entry);
                        entriesSinceCheckpoint++;
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex,
                            "Failed to write audit log entry: {EventType}",
                            entry.EventType);
                    }
                }

                if (ShouldCheckpoint(entriesSinceCheckpoint, lastCheckpointAt))
                {
                    Checkpoint(context);
                    EnforceMaxSize(context);
                    entriesSinceCheckpoint = 0;
                    lastCheckpointAt = clock.UtcNow;
                }
            }
        }
        catch (OperationCanceledException)
        {
            Logger.Information("AuditLogWriter stopping due to cancellation.");
        }

        DrainRemaining(context);
        Checkpoint(context);

        Logger.Information("AuditLogWriter stopped.");
    }

    private bool ShouldCheckpoint(
        int entriesSinceCheckpoint,
        DateTimeOffset lastCheckpointAt)
    {
        if (entriesSinceCheckpoint == 0)
            return false;

        return entriesSinceCheckpoint >= CheckpointAfterEntries
            || clock.UtcNow - lastCheckpointAt >= CheckpointInterval;
    }

    private void Checkpoint(SqliteWriteContext context)
    {
        try
        {
            context
                .Connection
                .NonQueryCmd(sql: "PRAGMA wal_checkpoint(TRUNCATE)")
                .Execute();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to checkpoint audit log WAL.");
        }
    }

    private void EnforceMaxSize(SqliteWriteContext context)
    {
        var maxSize = appSettings.AuditLogMaxSizeInBytes;

        if (maxSize is null or <= 0)
            return;

        try
        {
            var connection = context.Connection;
            var usedBytes = GetUsedBytes(connection);

            if (usedBytes <= maxSize.Value)
                return;

            var lowWater = (long)(maxSize.Value * MaxSizeLowWaterFraction);
            var totalDeleted = 0;
            var batches = 0;

            while (usedBytes > lowWater && batches < MaxSizeMaxBatchesPerCycle)
            {
                var deleted = DeleteOldestBatch(connection, MaxSizeDeleteBatch);

                if (deleted == 0)
                    break;

                totalDeleted += deleted;
                batches++;
                usedBytes = GetUsedBytes(connection);
            }

            if (totalDeleted == 0)
                return;

            Checkpoint(context);

            Logger.Information(
                "Audit log exceeded max size {MaxSizeBytes} B; deleted {DeletedCount} oldest entries, now {UsedBytes} B.",
                maxSize.Value,
                totalDeleted,
                usedBytes);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to enforce audit log max size.");
        }
    }

    private static long GetUsedBytes(SqliteConnection connection)
    {
        var pageSize = connection
            .OneRowCmd(sql: "PRAGMA page_size", readRowFunc: reader => reader.GetInt64(0))
            .Execute();

        var pageCount = connection
            .OneRowCmd(sql: "PRAGMA page_count", readRowFunc: reader => reader.GetInt64(0))
            .Execute();

        var freeCount = connection
            .OneRowCmd(sql: "PRAGMA freelist_count", readRowFunc: reader => reader.GetInt64(0))
            .Execute();

        if (pageSize.IsEmpty || pageCount.IsEmpty || freeCount.IsEmpty)
            return 0;

        return (pageCount.Value - freeCount.Value) * pageSize.Value;
    }

    private static int DeleteOldestBatch(SqliteConnection connection, int batchSize)
    {
        return connection
            .NonQueryCmd(
                sql: """
                    DELETE FROM al_audit_logs
                    WHERE al_id IN (
                        SELECT al_id
                        FROM al_audit_logs
                        ORDER BY al_id
                        LIMIT $batch
                    )
                    """)
            .WithParameter("$batch", batchSize)
            .Execute()
            .AffectedRows;
    }

    private void DrainRemaining(SqliteWriteContext context)
    {
        while (channel.Reader.TryRead(out var entry))
        {
            try
            {
                WriteEntry(context, entry);
            }
            catch (Exception ex)
            {
                Logger.Error(ex,
                    "Failed to write audit log entry during shutdown: {EventType}",
                    entry.EventType);
            }
        }
    }

    private void WriteEntry(
        SqliteWriteContext context,
        AuditLogEntry entry)
    {
        context
            .OneRowCmd(
                sql: """
                    INSERT INTO al_audit_logs (
                        al_external_id,
                        al_created_at,
                        al_correlation_id,
                        al_actor_identity_type,
                        al_actor_identity,
                        al_actor_email,
                        al_actor_ip,
                        al_event_category,
                        al_event_type,
                        al_event_severity,
                        al_workspace_external_id,
                        al_box_external_id,
                        al_box_link_external_id,
                        al_details
                    ) VALUES (
                        $externalId,
                        $createdAt,
                        $correlationId,
                        $actorIdentityType,
                        $actorIdentity,
                        $actorEmail,
                        $actorIp,
                        $eventCategory,
                        $eventType,
                        $eventSeverity,
                        $workspaceExternalId,
                        $boxExternalId,
                        $boxLinkExternalId,
                        $details
                    )
                    RETURNING al_id
                    """,
                readRowFunc: reader => reader.GetInt32(0))
            .WithParameter("$externalId", AuditLogExtId.NewId().Value)
            .WithParameter("$createdAt", clock.UtcNow)
            .WithParameter("$correlationId", entry.CorrelationId?.ToString())
            .WithParameter("$actorIdentityType", entry.Actor.IdentityType)
            .WithParameter("$actorIdentity", entry.Actor.Identity)
            .WithParameter("$actorEmail", entry.ActorEmail)
            .WithParameter("$actorIp", entry.ActorIp)
            .WithParameter("$eventCategory", entry.EventCategory)
            .WithParameter("$eventType", entry.EventType)
            .WithParameter("$eventSeverity", entry.Severity)
            .WithParameter("$workspaceExternalId", entry.WorkspaceExternalId)
            .WithParameter("$boxExternalId", entry.BoxExternalId)
            .WithParameter("$boxLinkExternalId", entry.BoxLinkExternalId)
            .WithParameter("$details", entry.DetailsJson)
            .Execute();
    }
}
