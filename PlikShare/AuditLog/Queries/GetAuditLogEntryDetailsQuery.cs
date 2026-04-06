using PlikShare.AuditLog.Contracts;
using PlikShare.AuditLog.Id;
using PlikShare.Core.Database.AuditLogDatabase;
using PlikShare.Core.SQLite;

namespace PlikShare.AuditLog.Queries;

public class GetAuditLogEntryDetailsQuery(PlikShareAuditLogDb plikShareAuditLogDb)
{
    public GetAuditLogEntryDetailsResponseDto? Execute(AuditLogExtId externalId)
    {
        using var connection = plikShareAuditLogDb.OpenConnection();

        var entries = connection
            .Cmd(
                sql: """
                    SELECT
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
                    FROM al_audit_logs
                    WHERE al_external_id = $externalId
                    LIMIT 1
                    """,
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
                })
            .WithParameter("$externalId", externalId.Value)
            .Execute();

        return entries.FirstOrDefault();
    }
}
