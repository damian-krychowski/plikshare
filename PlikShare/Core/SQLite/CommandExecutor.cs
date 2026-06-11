using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Data.Sqlite;
using PlikShare.Core.Utils;

namespace PlikShare.Core.SQLite;

public class SQLiteCommandExecutor<TRow>
{
    private readonly Func<SqliteDataReader, TRow> _readRowFunc;
    private readonly SqliteCommand _command;
    private readonly bool _shouldDispose;
    private readonly string _source;

    public SQLiteCommandExecutor(
        string commandText,
        Func<SqliteDataReader, TRow> readRowFunc,
        LazySqLiteCommandsPool commandsPool,
        string source,
        SqliteTransaction? transaction = null)
    {
        _readRowFunc = readRowFunc;
        _source = source;

        _command = commandsPool.GetOrCreate(commandText);
        _command.Transaction = transaction;
        _shouldDispose = false;
    }

    public SQLiteCommandExecutor(
        string commandText,
        Func<SqliteDataReader, TRow> readRowFunc,
        SqliteConnection connection,
        string source,
        SqliteTransaction? transaction = null)
    {
        _readRowFunc = readRowFunc;
        _source = source;

        _command = connection.CreateCommand();
        _command.Transaction = transaction;
        _command.CommandText = commandText;
        _shouldDispose = true;
    }

    public SQLiteCommandExecutor<TRow> WithParameter<T>(string name, T value)
    {
        _command.WithParameter(name, value);
        return this;
    }

    public SQLiteCommandExecutor<TRow> WithEnumParameter<T>(string name, T value) where T : Enum
    {
        _command.WithParameter(name, value.ToKebabCase());
        return this;
    }

    public SQLiteCommandExecutor<TRow> WithJsonParameter<T>(string name, T value)
    {
        _command.WithJsonParameter(name, value);
        return this;
    }

    public IEnumerable<TRow> ExecuteEnumerable()
    {
        var startTimestamp = Stopwatch.GetTimestamp();
        var rows = 0;
        var success = false;

        try
        {
            using var reader = _command.ExecuteReader();

            while (reader.Read())
            {
                var row = _readRowFunc(reader);
                rows++;
                yield return row;
            }

            reader.Close();
            success = true;
        }
        finally
        {
            SqliteQueryMetrics.Record(
                source: _source,
                kind: SqliteQueryMetrics.KindRows,
                startTimestamp: startTimestamp,
                rows: rows,
                success: success);

            if (_shouldDispose)
            {
                _command.Dispose();
            }
        }
    }

    public List<TRow> Execute()
    {
        var startTimestamp = Stopwatch.GetTimestamp();
        var rows = 0;
        var success = false;

        try
        {
            using var reader = _command.ExecuteReader();

            if (!reader.HasRows)
            {
                reader.Close();
                success = true;
                return [];
            }

            var results = new List<TRow>();

            while (reader.Read())
            {
                var row = _readRowFunc(reader);
                results.Add(row);
            }

            reader.Close();

            rows = results.Count;
            success = true;
            return results;
        }
        finally
        {
            SqliteQueryMetrics.Record(
                source: _source,
                kind: SqliteQueryMetrics.KindRows,
                startTimestamp: startTimestamp,
                rows: rows,
                success: success);

            if (_shouldDispose)
            {
                _command.Dispose();
            }
        }
    }
}

public static class SQLiteCommandExecutorExtensions
{
    public static SQLiteCommandExecutor<TRow> Cmd<TRow>(
        this SqliteConnection connection,
        string sql,
        Func<SqliteDataReader, TRow> readRowFunc,
        SqliteTransaction? transaction = null,
        string? name = null,
        [CallerFilePath] string? callerFilePath = null,
        [CallerMemberName] string? callerMember = null)
    {
        var command = new SQLiteCommandExecutor<TRow>(
            commandText: sql,
            connection: connection,
            transaction: transaction,
            readRowFunc: readRowFunc,
            source: SqliteQueryMetrics.ResolveSource(name, callerFilePath, callerMember));

        return command;
    }

    public static SQLiteCommandExecutor<TRow> Cmd<TRow>(
        this LazySqLiteCommandsPool commandsPool,
        string sql,
        Func<SqliteDataReader, TRow> readRowFunc,
        SqliteTransaction? transaction = null,
        string? name = null,
        [CallerFilePath] string? callerFilePath = null,
        [CallerMemberName] string? callerMember = null)
    {
        var command = new SQLiteCommandExecutor<TRow>(
            commandText: sql,
            commandsPool: commandsPool,
            transaction: transaction,
            readRowFunc: readRowFunc,
            source: SqliteQueryMetrics.ResolveSource(name, callerFilePath, callerMember));

        return command;
    }

    public static SQLiteCommandExecutor<TRow> Cmd<TRow>(
        this SqliteWriteContext dbWriteContext,
        string sql,
        Func<SqliteDataReader, TRow> readRowFunc,
        SqliteTransaction? transaction = null,
        string? name = null,
        [CallerFilePath] string? callerFilePath = null,
        [CallerMemberName] string? callerMember = null)
    {
        var command = new SQLiteCommandExecutor<TRow>(
            commandText: sql,
            commandsPool: dbWriteContext.CommandsPool,
            transaction: transaction,
            readRowFunc: readRowFunc,
            source: SqliteQueryMetrics.ResolveSource(name, callerFilePath, callerMember));

        return command;
    }
}
