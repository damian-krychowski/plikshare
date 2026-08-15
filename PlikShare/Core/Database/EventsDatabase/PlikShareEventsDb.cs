using Microsoft.Data.Sqlite;
using PlikShare.Core.SQLite;

namespace PlikShare.Core.Database.EventsDatabase;

/// <summary>
/// The agent-event firehose lives here, isolated from the operational main database. High-churn
/// inbox rows with their own write lane, WAL, retention and VACUUM never compete with — nor bloat —
/// the core. Configuration that governs delivery (subscriptions) stays in the main database.
/// </summary>
public class PlikShareEventsDb
{
    public static PlikShareDbType Type => PlikShareDbType.Events;

    private readonly string _connectionString;

    public PlikShareEventsDb(Volumes.Volumes volumes)
    {
        var dbPath = Path.Combine(
            volumes.Main.SQLite.FullPath,
            "plikshare_events.db");

        _connectionString = new SqliteConnectionStringBuilder($"Data Source={dbPath};")
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
