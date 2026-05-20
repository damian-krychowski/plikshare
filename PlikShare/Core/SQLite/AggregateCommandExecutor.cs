using System.Text;
using Microsoft.Data.Sqlite;
using PlikShare.Core.Encryption;
using PlikShare.Core.Utils;

namespace PlikShare.Core.SQLite;

/// <summary>
/// Sibling of <see cref="SQLiteCommandExecutor{TRow}"/> that folds the result set into a
/// single accumulator instead of materializing a <c>List&lt;TRow&gt;</c>. Each row is applied
/// to the accumulator via <c>aggregateRowFunc</c>; <see cref="Execute"/> returns the final
/// accumulator. Use it when the caller wants a grouped/reduced shape (e.g. a dictionary of
/// lists) and the intermediate row list would just be looped over once and thrown away.
/// </summary>
public class SQLiteAggregateCommandExecutor<TAccumulator>
{
    private readonly TAccumulator _seed;
    private readonly Func<TAccumulator, SqliteDataReader, TAccumulator> _aggregateRowFunc;
    private readonly SqliteCommand _command;
    private readonly bool _shouldDispose;

    public SQLiteAggregateCommandExecutor(
        string commandText,
        TAccumulator seed,
        Func<TAccumulator, SqliteDataReader, TAccumulator> aggregateRowFunc,
        LazySqLiteCommandsPool commandsPool,
        SqliteTransaction? transaction = null)
    {
        _seed = seed;
        _aggregateRowFunc = aggregateRowFunc;

        _command = commandsPool.GetOrCreate(commandText);
        _command.Transaction = transaction;
        _shouldDispose = false;
    }

    public SQLiteAggregateCommandExecutor(
        string commandText,
        TAccumulator seed,
        Func<TAccumulator, SqliteDataReader, TAccumulator> aggregateRowFunc,
        SqliteConnection connection,
        SqliteTransaction? transaction = null)
    {
        _seed = seed;
        _aggregateRowFunc = aggregateRowFunc;

        _command = connection.CreateCommand();
        _command.Transaction = transaction;
        _command.CommandText = commandText;
        _shouldDispose = true;
    }

    public SQLiteAggregateCommandExecutor<TAccumulator> WithParameter<T>(string name, T value)
    {
        _command.WithParameter(name, value);
        return this;
    }

    public SQLiteAggregateCommandExecutor<TAccumulator> WithEnumParameter<T>(string name, T value) where T : Enum
    {
        _command.WithParameter(name, value.ToKebabCase());
        return this;
    }

    public SQLiteAggregateCommandExecutor<TAccumulator> WithJsonParameter<T>(string name, T value)
    {
        _command.WithJsonParameter(name, value);
        return this;
    }

    public SQLiteAggregateCommandExecutor<TAccumulator> WithEncryptableParameter(string name, EncryptableMetadata metadata)
    {
        _command.WithParameter(name, metadata.Encode().Encoded);
        return this;
    }

    public SQLiteAggregateCommandExecutor<TAccumulator> WithEncryptableBlobParameter(string name, EncryptableMetadata metadata)
    {
        var bytes = Encoding.UTF8.GetBytes(metadata.Encode().Encoded);
        _command.WithParameter(name, bytes);
        return this;
    }

    public SQLiteAggregateCommandExecutor<TAccumulator> WithEncryptableBlobParameterOrNull(string name, EncryptableMetadata? metadata)
    {
        if (metadata is null)
        {
            _command.WithParameter<byte[]?>(name, null);
            return this;
        }

        var bytes = Encoding.UTF8.GetBytes(metadata.Value.Encode().Encoded);
        _command.WithParameter(name, bytes);
        return this;
    }

    public TAccumulator Execute()
    {
        try
        {
            using var reader = _command.ExecuteReader();

            var accumulator = _seed;

            if (!reader.HasRows)
            {
                reader.Close();
                return accumulator;
            }

            while (reader.Read())
                accumulator = _aggregateRowFunc(accumulator, reader);

            reader.Close();

            return accumulator;
        }
        finally
        {
            if (_shouldDispose)
                _command.Dispose();
        }
    }
}

public static class SQLiteAggregateCommandExecutorExtensions
{
    public static SQLiteAggregateCommandExecutor<TAccumulator> AggregateRows<TAccumulator>(
        this SqliteConnection connection,
        string sql,
        TAccumulator seed,
        Func<TAccumulator, SqliteDataReader, TAccumulator> aggregateRowFunc,
        SqliteTransaction? transaction = null)
        => new(
            commandText: sql,
            seed: seed,
            aggregateRowFunc: aggregateRowFunc,
            connection: connection,
            transaction: transaction);

    public static SQLiteAggregateCommandExecutor<TAccumulator> AggregateRows<TAccumulator>(
        this LazySqLiteCommandsPool commandsPool,
        string sql,
        TAccumulator seed,
        Func<TAccumulator, SqliteDataReader, TAccumulator> aggregateRowFunc,
        SqliteTransaction? transaction = null)
        => new(
            commandText: sql,
            seed: seed,
            aggregateRowFunc: aggregateRowFunc,
            commandsPool: commandsPool,
            transaction: transaction);

    public static SQLiteAggregateCommandExecutor<TAccumulator> AggregateRows<TAccumulator>(
        this SqliteWriteContext dbWriteContext,
        string sql,
        TAccumulator seed,
        Func<TAccumulator, SqliteDataReader, TAccumulator> aggregateRowFunc,
        SqliteTransaction? transaction = null)
        => new(
            commandText: sql,
            seed: seed,
            aggregateRowFunc: aggregateRowFunc,
            commandsPool: dbWriteContext.CommandsPool,
            transaction: transaction);
}
