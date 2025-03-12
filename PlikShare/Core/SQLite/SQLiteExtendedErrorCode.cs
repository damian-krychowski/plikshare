using Microsoft.Data.Sqlite;

namespace PlikShare.Core.SQLite;

public class SQLiteExtendedErrorCode
{
    public const int ConstraintForeignKey = 787;
    public const int ConstraintUnique = 2067;
    public const int ConstraintNotNull = 1299;
}

public static class SQLiteExceptionExtensions 
{
    public static bool HasForeignKeyFailed(
        this SqliteException exception)
    {
        return exception.SqliteExtendedErrorCode == SQLiteExtendedErrorCode.ConstraintForeignKey;
    }
    
    public static bool HasUniqueConstraintFailed(
        this SqliteException exception,
        string tableName,
        string columnName)
    {
        if (exception.SqliteExtendedErrorCode != SQLiteExtendedErrorCode.ConstraintUnique)
            return false;

        return exception.Message.Contains($"UNIQUE constraint failed: {tableName}.{columnName}");
    }
    
    public static bool HasNotNullConstraintFailed(
        this SqliteException exception,
        string tableName,
        string columnName)
    {
        if (exception.SqliteExtendedErrorCode != SQLiteExtendedErrorCode.ConstraintNotNull)
            return false;

        return exception.Message.Contains($"NOT NULL constraint failed: {tableName}.{columnName}");
    }
}