using Microsoft.Data.Sqlite;
using PlikShare.Core.Database.AiDatabase;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Utils;

namespace PlikShare.Core.SQLite;

public class SQLiteCommandExecutor<TRow>
{
    private readonly Func<SqliteDataReader, TRow> _readRowFunc;
    private readonly SqliteCommand _command;
    private readonly bool _shouldDispose;

    public SQLiteCommandExecutor(
        string commandText,
        Func<SqliteDataReader, TRow> readRowFunc,
        LazySqLiteCommandsPool commandsPool,
        SqliteTransaction? transaction = null)
    {
        _readRowFunc = readRowFunc;

        _command = commandsPool.GetOrCreate(commandText);
        _command.Transaction = transaction;
        _shouldDispose = false;
    }

    public SQLiteCommandExecutor(
        string commandText,
        Func<SqliteDataReader, TRow> readRowFunc,
        SqliteConnection connection, 
        SqliteTransaction? transaction = null)
    {
        _readRowFunc = readRowFunc;

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
    public List<TRow> Execute()
    {
        try
        {
            using var reader = _command.ExecuteReader();

            if (!reader.HasRows)
            {
                reader.Close();
                return [];
            }

            var results = new List<TRow>();

            while (reader.Read())
            {
                var row = _readRowFunc(reader);
                results.Add(row);
            }

            reader.Close();

            return results;
        }
        finally
        {
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
        SqliteTransaction? transaction = null)
    {
        var command = new SQLiteCommandExecutor<TRow>(
            commandText: sql,
            connection: connection,
            transaction: transaction,
            readRowFunc: readRowFunc);

        return command;
    }

    public static SQLiteCommandExecutor<TRow> Cmd<TRow>(
        this LazySqLiteCommandsPool commandsPool,
        string sql,
        Func<SqliteDataReader, TRow> readRowFunc,
        SqliteTransaction? transaction = null)
    {
        var command = new SQLiteCommandExecutor<TRow>(
            commandText: sql,
            commandsPool: commandsPool,
            transaction: transaction,
            readRowFunc: readRowFunc);

        return command;
    }

    public static SQLiteCommandExecutor<TRow> Cmd<TRow>(
        this DbWriteQueue.Context dbWriteContext,
        string sql,
        Func<SqliteDataReader, TRow> readRowFunc,
        SqliteTransaction? transaction = null)
    {
        var command = new SQLiteCommandExecutor<TRow>(
            commandText: sql,
            commandsPool: dbWriteContext.CommandsPool,
            transaction: transaction,
            readRowFunc: readRowFunc);

        return command;
    }

    public static SQLiteCommandExecutor<TRow> Cmd<TRow>(
        this AiDbWriteQueue.Context dbWriteContext,
        string sql,
        Func<SqliteDataReader, TRow> readRowFunc,
        SqliteTransaction? transaction = null)
    {
        var command = new SQLiteCommandExecutor<TRow>(
            commandText: sql,
            commandsPool: dbWriteContext.CommandsPool,
            transaction: transaction,
            readRowFunc: readRowFunc);

        return command;
    }
}