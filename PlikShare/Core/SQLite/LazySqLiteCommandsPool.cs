using Microsoft.Data.Sqlite;

namespace PlikShare.Core.SQLite;

public class LazySqLiteCommandsPool(SqliteConnection connection): IDisposable
{
    private readonly Dictionary<string, SqliteCommand> _sqliteCommands = new();

    public SqliteCommand GetOrCreate(string commandText)
    {
        return _sqliteCommands.ContainsKey(commandText) 
            ? GetExistingCommand(commandText) 
            : CreateNewCommand(commandText);
    }

    private SqliteCommand GetExistingCommand(string commandText)
    {
        var cmd = _sqliteCommands[commandText];
        cmd.Parameters.Clear();
        cmd.Transaction = null;

        return cmd;
    }

    private SqliteCommand CreateNewCommand(string commandText)
    {
        var command = connection.CreateCommand();
        command.CommandText = commandText;

        _sqliteCommands[commandText] = command;

        return command;
    }

    public void Dispose()
    {
        foreach (var sqliteCommand in _sqliteCommands)
        {
            sqliteCommand.Value.Dispose();
        }
    }
}

public static class LazySqLiteCommandsPoolExtensions
{
    public static LazySqLiteCommandsPool CreateLazyCommandsPool(
        this SqliteConnection connection)
    {
        return new LazySqLiteCommandsPool(connection);
    }
}