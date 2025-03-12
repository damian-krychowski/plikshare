using Microsoft.Data.Sqlite;
using PlikShare.Core.SQLite;

namespace PlikShare.Core.Database.MainDatabase;

public class PlikShareDb
{
    public static PlikShareDbType Type => PlikShareDbType.Main;

    private readonly string _connectionString;

    public PlikShareDb(Volumes.Volumes volumes)
    {
        var dbPath = Path.Combine(
            volumes.Main.SQLite.FullPath, 
            "plikshare.db");
        
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