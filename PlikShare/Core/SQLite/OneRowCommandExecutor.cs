using System.Text;
using Microsoft.Data.Sqlite;
using PlikShare.Core.Database.AiDatabase;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Utils;

namespace PlikShare.Core.SQLite;

public readonly record struct SQLiteOneRowCommandResult<TRow>(
    bool IsEmpty,
    in TRow Value)
{
    
}

public class SQLiteOneRowCommandExecutor<TRow>
{
    private readonly Func<SqliteDataReader, TRow> _readRowFunc;
    private readonly SqliteCommand _command;
    private readonly bool _shouldDispose;

    public SQLiteOneRowCommandExecutor(
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

    public SQLiteOneRowCommandExecutor(
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

    public SQLiteOneRowCommandExecutor<TRow> WithBlobParameter(string name, string value)
    {
        var byteArray = Encoding.UTF8.GetBytes(value);
        _command.WithParameter(name, byteArray);
        return this;
    }

    public SQLiteOneRowCommandExecutor<TRow> WithParameter<T>(string name, T value)
    {
        _command.WithParameter(name, value);
        return this;
    }

    public SQLiteOneRowCommandExecutor<TRow> WithEnumParameter<T>(string name, T value) where T: Enum
    {
        _command.WithParameter(name, value.ToKebabCase());
        return this;
    }

    public SQLiteOneRowCommandExecutor<TRow> WithJsonParameter<T>(string name, T value)
    {
        _command.WithJsonParameter(name, value);
        return this;
    }

    public TRow ExecuteOrThrow()
    {
        var result = Execute();

        if (result.IsEmpty)
        {
            throw new InvalidOperationException(
                $"Something went wrong while executing SQL command: '{_command.CommandText}'");
        }

        return result.Value;
    }

    public TRow? ExecuteOrValue(TRow? valueIfEmpty)
    {
        var result = Execute();

        return result.IsEmpty 
            ? valueIfEmpty 
            : result.Value;
    }

    public SQLiteOneRowCommandResult<TRow> Execute()
    {
        try
        {
            using var reader = _command.ExecuteReader();

            if (!reader.HasRows)
            {
                return new SQLiteOneRowCommandResult<TRow>(
                    IsEmpty: true,
                    Value: default!);
            }

            reader.Read();
            var row = _readRowFunc(reader);

            return new SQLiteOneRowCommandResult<TRow>(
                IsEmpty: false,
                Value: row);
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

public static class SQLiteOneRowCommandExecutorExtensions
{
    public static SQLiteOneRowCommandExecutor<TRow> OneRowCmd<TRow>(
        this SqliteConnection connection, 
        string sql,
        Func<SqliteDataReader, TRow> readRowFunc,
        SqliteTransaction? transaction = null)
    {
        var command = new SQLiteOneRowCommandExecutor<TRow>(
            commandText: sql,
            connection: connection,
            transaction: transaction,
            readRowFunc: readRowFunc);

        return command;
    }

    public static SQLiteOneRowCommandExecutor<TRow> OneRowCmd<TRow>(
        this DbWriteQueue.Context dbWriteQueueContext,
        string sql,
        Func<SqliteDataReader, TRow> readRowFunc,
        SqliteTransaction? transaction = null)
    {
        var command = new SQLiteOneRowCommandExecutor<TRow>(
            commandText: sql,
            commandsPool: dbWriteQueueContext.CommandsPool,
            transaction: transaction,
            readRowFunc: readRowFunc);

        return command;
    }



    public static SQLiteOneRowCommandExecutor<TRow> OneRowCmd<TRow>(
        this AiDbWriteQueue.Context dbWriteQueueContext,
        string sql,
        Func<SqliteDataReader, TRow> readRowFunc,
        SqliteTransaction? transaction = null)
    {
        var command = new SQLiteOneRowCommandExecutor<TRow>(
            commandText: sql,
            commandsPool: dbWriteQueueContext.CommandsPool,
            transaction: transaction,
            readRowFunc: readRowFunc);

        return command;
    }
}