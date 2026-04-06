using Microsoft.Data.Sqlite;
using PlikShare.Core.SQLite;

namespace PlikShare.Core.Database.AuditLogDatabase;

public class PlikShareAuditLogDb
{
    public static PlikShareDbType Type => PlikShareDbType.AuditLog;

    private readonly string _connectionString;
    public string DbPath { get; }

    public PlikShareAuditLogDb(Volumes.Volumes volumes)
    {
        DbPath = Path.Combine(
            volumes.Main.SQLite.FullPath,
            "plikshare_audit_log.db");

        _connectionString = new SqliteConnectionStringBuilder($"Data Source={DbPath};")
        {
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = true,
            ForeignKeys = true,
        }.ToString();
    }

    public SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);

        connection.Open();

        connection
            .NonQueryCmd("PRAGMA synchronous=NORMAL")
            .Execute();

        return connection;
    }

    public SqliteConnection OpenInitialConnection()
    {
        var connection = OpenConnection();

        connection
            .NonQueryCmd(sql: "PRAGMA journal_mode=WAL")
            .Execute();

        return connection;
    }
}
