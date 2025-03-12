using Microsoft.Data.Sqlite;
using PlikShare.Core.SQLite;

namespace PlikShare.Core.Database.AiDatabase;

public class PlikShareAiDb
{
    public static PlikShareDbType Type => PlikShareDbType.Ai;

    private readonly string _connectionString;

    public PlikShareAiDb(Volumes.Volumes volumes)
    {
        var dbPath = Path.Combine(
            volumes.Main.SQLite.FullPath, 
            "plikshare_ai.db");
        
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