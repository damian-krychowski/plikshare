using PlikShare.AuditLog.Contracts;
using PlikShare.Core.Database.AuditLogDatabase;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;

namespace PlikShare.AuditLog.Queries;

public class GetAuditLogFilterOptionsQuery(
    PlikShareAuditLogDb plikShareAuditLogDb,
    PlikShareDb plikShareDb)
{
    public AuditLogFilterOptionsDto Execute()
    {
        using var auditLogConnection = plikShareAuditLogDb.OpenConnection();

        var actors = auditLogConnection
            .Cmd(
                sql: """
                    SELECT DISTINCT al_actor_email
                    FROM al_audit_logs
                    WHERE al_actor_email IS NOT NULL
                    ORDER BY al_actor_email
                    LIMIT 500
                    """,
                readRowFunc: reader => reader.GetString(0))
            .Execute();

        using var mainConnection = plikShareDb.OpenConnection();

        var agents = mainConnection
            .Cmd(
                sql: """
                    SELECT a_external_id, a_name
                    FROM a_agents
                    ORDER BY a_name
                    """,
                readRowFunc: reader => new AuditLogAgentActorDto
                {
                    ExternalId = reader.GetString(0),
                    Name = reader.GetString(1)
                })
            .Execute();

        return new AuditLogFilterOptionsDto
        {
            EventTypes = AuditLogEventTypes.All.ToList(),
            Actors = actors,
            Agents = agents
        };
    }
}
