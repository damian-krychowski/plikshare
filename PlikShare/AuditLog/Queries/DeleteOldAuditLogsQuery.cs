using PlikShare.Core.Database.AuditLogDatabase;
using PlikShare.Core.SQLite;

namespace PlikShare.AuditLog.Queries;

public class DeleteOldAuditLogsQuery(PlikShareAuditLogDb plikShareAuditLogDb)
{
    public int Execute(string olderThanDate)
    {
        using var connection = plikShareAuditLogDb.OpenConnection();

        var result = connection
            .NonQueryCmd(
                sql: "DELETE FROM al_audit_logs WHERE al_created_at < $cutoff")
            .WithParameter("$cutoff", olderThanDate)
            .Execute();

        return result.AffectedRows;
    }
}
