using System.Text;
using Microsoft.Data.Sqlite;
using PlikShare.Core.Encryption;
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

    /// <summary>
    /// Binds an <see cref="EncryptableMetadata"/> value: for plaintext-mode workspaces
    /// the raw string is bound; for full-encryption workspaces the value is AES-GCM
    /// encrypted under the workspace DEK and bound as base64 of the envelope. All the
    /// mode-switching logic lives inside <see cref="EncryptableMetadataExtensions.Encode"/>;
    /// queries stay oblivious to encryption.
    /// </summary>
    public SQLiteCommandExecutor<TRow> WithEncryptableParameter(string name, EncryptableMetadata metadata)
    {
        _command.WithParameter(name, metadata.Encode().Encoded);
        return this;
    }

    /// <summary>
    /// BLOB-affinity sibling of <see cref="WithEncryptableParameter"/>: encodes the value
    /// the same way (plaintext for non-encrypted workspaces, <c>pse:</c>-prefixed envelope
    /// for full-encryption workspaces) but binds the result as UTF-8 bytes. Use for
    /// columns declared BLOB whose readers go through
    /// <see cref="DbReaderExtensions.GetStringFromBlob"/>.
    /// </summary>
    public SQLiteCommandExecutor<TRow> WithEncryptableBlobParameter(string name, EncryptableMetadata metadata)
    {
        var bytes = Encoding.UTF8.GetBytes(metadata.Encode().Encoded);
        _command.WithParameter(name, bytes);
        return this;
    }

    /// <summary>
    /// Nullable variant of <see cref="WithEncryptableBlobParameter"/> for nullable BLOB
    /// columns. A null metadata binds NULL; otherwise behaves identically.
    /// </summary>
    public SQLiteCommandExecutor<TRow> WithEncryptableBlobParameterOrNull(string name, EncryptableMetadata? metadata)
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
        this SqliteWriteContext dbWriteContext,
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