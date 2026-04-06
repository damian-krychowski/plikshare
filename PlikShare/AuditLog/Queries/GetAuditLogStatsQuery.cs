using PlikShare.AuditLog.Contracts;
using PlikShare.Core.Database.AuditLogDatabase;
using PlikShare.Core.SQLite;

namespace PlikShare.AuditLog.Queries;

public class GetAuditLogStatsQuery(PlikShareAuditLogDb plikShareAuditLogDb)
{
    public AuditLogStatsResponseDto Execute()
    {
        var dbFileInfo = new FileInfo(plikShareAuditLogDb.DbPath);
        var dbSizeInBytes = dbFileInfo.Exists ? dbFileInfo.Length : 0;

        using var connection = plikShareAuditLogDb.OpenConnection();

        var stats = connection
            .OneRowCmd(
                sql: """
                    SELECT
                        COUNT(*),
                        MIN(al_created_at),
                        MAX(al_created_at)
                    FROM al_audit_logs
                    """,
                readRowFunc: reader => new
                {
                    TotalCount = reader.GetInt32(0),
                    OldestDate = reader.GetStringOrNull(1),
                    NewestDate = reader.GetStringOrNull(2)
                })
            .Execute();

        return new AuditLogStatsResponseDto
        {
            DbSizeInBytes = dbSizeInBytes,
            TotalLogCount = stats.IsEmpty ? 0 : stats.Value.TotalCount,
            OldestEntryDate = stats.IsEmpty ? null : stats.Value.OldestDate,
            NewestEntryDate = stats.IsEmpty ? null : stats.Value.NewestDate
        };
    }
}
