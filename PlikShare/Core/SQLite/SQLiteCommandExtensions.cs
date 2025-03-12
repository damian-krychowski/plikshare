using Microsoft.Data.Sqlite;
using PlikShare.Core.Utils;

namespace PlikShare.Core.SQLite;

public static class SqLiteCommandExtensions
{
    public static void WithParameter<T>(this SqliteCommand command, string name, T value)
    {
        switch (value)
        {
            case null: 
                command.Parameters.AddWithValue(name, DBNull.Value);
                break;
            
            case DateTimeOffset dateTimeOffset:
                AddDateTimeOffset(command, name, dateTimeOffset);
                break;
            
            case DateTime:
                throw new InvalidOperationException(
                    $"Queue should operate on {nameof(DateTimeOffset)} type only");
            
            default:
                command.Parameters.AddWithValue(name, value);
                break;
        }
    }
    
    private static void AddDateTimeOffset(SqliteCommand command, string name, DateTimeOffset dateTimeOffset)
    {
        if (dateTimeOffset.Offset > TimeSpan.Zero)
            throw new InvalidOperationException(
                $"Queue should operate entirely on UTC dates, but found: {dateTimeOffset}");
        
        command.Parameters.AddWithValue(name, dateTimeOffset);
    }

    public static void  WithJsonParameter<T>(
        this SqliteCommand command, 
        string name, 
        T value)
    {
        switch (value)
        {
            case null: 
                command.Parameters.AddWithValue(name, DBNull.Value);
                break;

            default:
                command.Parameters.AddWithValue(name, Json.Serialize(value));
                break;
        }
    }

    public static List<TRow> GetRows<TRow>(
        this SqliteCommand command,
        Func<SqliteDataReader, TRow> readRowFunc)
    {
        using var reader = command.ExecuteReader();

        if (!reader.HasRows)
        {
            reader.Close();
            return [];
        }

        var results = new List<TRow>();

        while (reader.Read())
        {
            var row = readRowFunc(reader);
            results.Add(row);
        }

        reader.Close();

        return results;
    }
}