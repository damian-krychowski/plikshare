using PlikShare.AuditLog.Contracts;
using PlikShare.Core.Database.AuditLogDatabase;
using PlikShare.Core.SQLite;

namespace PlikShare.AuditLog.Queries;

public class GetAuditLogFilterOptionsQuery(PlikShareAuditLogDb plikShareAuditLogDb)
{
    public AuditLogFilterOptionsDto Execute()
    {
        using var connection = plikShareAuditLogDb.OpenConnection();

        var actors = connection
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

        return new AuditLogFilterOptionsDto
        {
            EventTypes = AuditLogEventTypes.All.ToList(),
            Actors = actors
        };
    }
}
