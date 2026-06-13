using Microsoft.Data.Sqlite;
using PlikShare.AuditLog.Contracts;
using PlikShare.Core.Database.AuditLogDatabase;
using PlikShare.Core.SQLite;
using PlikShare.GeneralSettings;

namespace PlikShare.AuditLog.Queries;

public class CompactAuditLogQuery(
    PlikShareAuditLogDb plikShareAuditLogDb,
    AppSettings appSettings)
{
    private const int DeleteBatch = 5000;

    public CompactAuditLogResponseDto Execute()
    {
        using var connection = plikShareAuditLogDb.OpenConnection();

        connection
            .NonQueryCmd(sql: "PRAGMA busy_timeout=120000")
            .Execute();

        var deletedCount = TrimToMaxSize(connection);

        connection
            .NonQueryCmd(sql: "VACUUM")
            .Execute();

        connection
            .NonQueryCmd(sql: "PRAGMA wal_checkpoint(TRUNCATE)")
            .Execute();

        var dbFileInfo = new FileInfo(plikShareAuditLogDb.DbPath);
        var dbSizeInBytes = dbFileInfo.Exists ? dbFileInfo.Length : 0;

        return new CompactAuditLogResponseDto
        {
            DeletedCount = deletedCount,
            DbSizeInBytes = dbSizeInBytes
        };
    }

    private int TrimToMaxSize(SqliteConnection connection)
    {
        var maxSize = appSettings.AuditLogMaxSizeInBytes;

        if (maxSize is null or <= 0)
            return 0;

        var totalDeleted = 0;
        var usedBytes = GetUsedBytes(connection);

        while (usedBytes > maxSize.Value)
        {
            var deleted = DeleteOldestBatch(connection, DeleteBatch);

            if (deleted == 0)
                break;

            totalDeleted += deleted;
            usedBytes = GetUsedBytes(connection);
        }

        return totalDeleted;
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
}
