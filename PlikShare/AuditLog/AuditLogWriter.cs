using PlikShare.AuditLog.Id;
using PlikShare.Core.Clock;
using PlikShare.Core.Database.AuditLogDatabase;
using PlikShare.Core.SQLite;
using Serilog;

namespace PlikShare.AuditLog;

public class AuditLogWriter(
    AuditLogChannel channel,
    PlikShareAuditLogDb plikShareAuditLogDb,
    IClock clock) : BackgroundService
{
    private static readonly Serilog.ILogger Logger = Log.ForContext<AuditLogWriter>();

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

        try
        {
            await foreach (var entry in channel.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    WriteEntry(context, entry);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex,
                        "Failed to write audit log entry: {EventType}",
                        entry.EventType);
                }
            }
        }
        catch (OperationCanceledException)
        {
            Logger.Information("AuditLogWriter stopping due to cancellation.");
        }

        DrainRemaining(context);

        Logger.Information("AuditLogWriter stopped.");
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
            .WithParameter("$details", entry.DetailsJson)
            .Execute();
    }
}
