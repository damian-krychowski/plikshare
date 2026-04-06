using System.Text.Json;
using PlikShare.AuditLog.Contracts;
using PlikShare.Core.Clock;
using PlikShare.Core.Database.AuditLogDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Core.Volumes;

namespace PlikShare.AuditLog.Queries;

public class ArchiveAuditLogsQuery(
    PlikShareAuditLogDb plikShareAuditLogDb,
    PlikShare.Core.Volumes.Volumes volumes,
    IClock clock)
{
    public ArchiveAuditLogsResponseDto Execute(string? olderThanDate)
    {
        var archiveDir = Path.Combine(
            volumes.Main.SQLite.FullPath,
            "audit-log-archives");

        Directory.CreateDirectory(archiveDir);

        var timestamp = clock.UtcNow.ToString("yyyyMMdd-HHmmss");
        var fileName = $"audit-log-archive-{timestamp}.json";
        var filePath = Path.Combine(archiveDir, fileName);

        using var connection = plikShareAuditLogDb.OpenConnection();

        var sql = olderThanDate is not null
            ? "SELECT al_external_id, al_created_at, al_correlation_id, al_actor_identity_type, al_actor_identity, al_actor_email, al_actor_ip, al_event_category, al_event_type, al_event_severity, al_workspace_external_id, al_details FROM al_audit_logs WHERE al_created_at < $cutoff ORDER BY al_id ASC"
            : "SELECT al_external_id, al_created_at, al_correlation_id, al_actor_identity_type, al_actor_identity, al_actor_email, al_actor_ip, al_event_category, al_event_type, al_event_severity, al_workspace_external_id, al_details FROM al_audit_logs ORDER BY al_id ASC";

        var items = connection
            .Cmd(
                sql: sql,
                readRowFunc: reader => new GetAuditLogEntryDetailsResponseDto
                {
                    ExternalId = reader.GetString(0),
                    CreatedAt = reader.GetString(1),
                    CorrelationId = reader.GetStringOrNull(2),
                    ActorIdentityType = reader.GetString(3),
                    ActorIdentity = reader.GetString(4),
                    ActorEmail = reader.GetStringOrNull(5),
                    ActorIp = reader.GetStringOrNull(6),
                    EventCategory = reader.GetString(7),
                    EventType = reader.GetString(8),
                    EventSeverity = reader.GetString(9),
                    WorkspaceExternalId = reader.GetStringOrNull(10),
                    Details = reader.GetStringOrNull(11)
                });

        if (olderThanDate is not null)
        {
            items = items.WithParameter("$cutoff", olderThanDate);
        }

        var results = items.Execute();

        using var fileStream = File.Create(filePath);
        JsonSerializer.Serialize(fileStream, results, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        return new ArchiveAuditLogsResponseDto
        {
            FileName = fileName,
            ArchivedCount = results.Count
        };
    }
}
