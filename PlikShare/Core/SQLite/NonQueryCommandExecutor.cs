using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Data.Sqlite;

namespace PlikShare.Core.SQLite;

public class SQLiteNonQueryCommandExecutor
{
    private readonly SqliteCommand _command;
    private readonly string _source;

    public SQLiteNonQueryCommandExecutor(
        string commandText,
        SqliteConnection connection,
        string source,
        SqliteTransaction? transaction = null)
    {
        _command = connection.CreateCommand();
        _command.Transaction = transaction;
        _command.CommandText = commandText;
        _source = source;
    }

    public SQLiteNonQueryCommandExecutor WithParameter<T>(string name, T value)
    {
        _command.WithParameter(name, value);
        return this;
    }

    public SQLiteNonQueryCommandResult Execute()
    {
        var startTimestamp = Stopwatch.GetTimestamp();
        var affectedRows = 0;
        var success = false;

        using var command = _command;

        try
        {
            affectedRows = command.ExecuteNonQuery();
            success = true;

            return new SQLiteNonQueryCommandResult(
                AffectedRows: affectedRows);
        }
        finally
        {
            SqliteQueryMetrics.Record(
                source: _source,
                kind: SqliteQueryMetrics.KindNonQuery,
                startTimestamp: startTimestamp,
                rows: affectedRows,
                success: success);
        }
    }
}

public readonly record struct SQLiteNonQueryCommandResult(
    int AffectedRows);

public static class SQLiteNonQueryCommandExecutorExtensions
{
    public static SQLiteNonQueryCommandExecutor NonQueryCmd(
        this SqliteConnection connection,
        string sql,
        SqliteTransaction? transaction = null,
        string? name = null,
        [CallerFilePath] string? callerFilePath = null,
        [CallerMemberName] string? callerMember = null)
    {
        return new SQLiteNonQueryCommandExecutor(
            commandText: sql,
            connection: connection,
            transaction: transaction,
            source: SqliteQueryMetrics.ResolveSource(name, callerFilePath, callerMember));
    }
}
