using Microsoft.Data.Sqlite;

namespace PlikShare.Core.SQLite;

public class SqliteWriteContext
{
    public required SqliteConnection Connection { get; init; }
    public required LazySqLiteCommandsPool CommandsPool { get; init; }
}