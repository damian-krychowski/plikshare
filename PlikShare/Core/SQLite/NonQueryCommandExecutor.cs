using Microsoft.Data.Sqlite;

namespace PlikShare.Core.SQLite;

public class SQLiteNonQueryCommandExecutor
{
    private readonly SqliteCommand _command;

    public SQLiteNonQueryCommandExecutor(
        string commandText,
        SqliteConnection connection, 
        SqliteTransaction? transaction = null)
    {
        _command = connection.CreateCommand();
        _command.Transaction = transaction;
        _command.CommandText = commandText;
    }
    
    public SQLiteNonQueryCommandExecutor WithParameter<T>(string name, T value)
    {
        _command.Parameters.AddWithValue(name, value);
        return this;
    }
    
    public SQLiteNonQueryCommandResult Execute()
    {
        using var command = _command;
        var affectedRows = command.ExecuteNonQuery();
        
        return new SQLiteNonQueryCommandResult(
            AffectedRows: affectedRows);
    }
}

public readonly record struct SQLiteNonQueryCommandResult(
    int AffectedRows);

public static class SQLiteNonQueryCommandExecutorExtensions
{
    public static SQLiteNonQueryCommandExecutor NonQueryCmd(
        this SqliteConnection connection, 
        string sql,
        SqliteTransaction? transaction = null)
    {
        return new SQLiteNonQueryCommandExecutor(
            commandText: sql,
            connection: connection,
            transaction: transaction);
    }
}