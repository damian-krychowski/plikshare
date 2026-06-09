using Microsoft.Data.Sqlite;

namespace PlikShare.Core.SQLite;

public class SqliteWriteContext
{
    private readonly HashSet<string> _registeredFunctions = [];

    public required SqliteConnection Connection { get; init; }
    public required LazySqLiteCommandsPool CommandsPool { get; init; }

    public bool TryClaimFunctionRegistration(string name)
    {
        return _registeredFunctions.Add(name);
    }
}