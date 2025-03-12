using Microsoft.Data.Sqlite;
using PlikShare.Core.Database;
using PlikShare.Core.Utils;

namespace PlikShare.Core.SQLite;

public interface ISQLiteMigration
{
    string Name { get; }
    DateOnly Date { get; }
    PlikShareDbType Type { get; }

    void Run(SqliteConnection connection, SqliteTransaction transaction);
}

public abstract class SQLiteMigrationBase : ISQLiteMigration
{
    public abstract string Name { get; }
    public abstract DateOnly Date { get; }
    public abstract PlikShareDbType Type { get; }

    protected abstract string[] ManifestResourceNames { get; }

    private string[] GetUpScripts()
    {
        return ManifestResourceNames
            .Select(ManifestResourceReader.Read)
            .ToArray();
    }

    public void Run(SqliteConnection connection, SqliteTransaction transaction)
    {
        var upScripts = GetUpScripts();

        foreach (var upScript in upScripts)
        {
            connection
                .NonQueryCmd(
                    sql: upScript,
                    transaction: transaction)
                .Execute();
        }
    }
}