using Microsoft.Data.Sqlite;
using PlikShare.Core.Utils;

namespace PlikShare.Core.SQLite;

public static class SQLiteConnectionExtensions
{
    public static void RegisterJsonArrayToBlobFunction(this SqliteConnection connection)
    {
        connection.CreateFunction(
            "app_json_array_to_blob",
            (string? jsonArray) =>
            {
                if (string.IsNullOrWhiteSpace(jsonArray))
                    return null;

                // Parse JSON array string into byte array
                return Json.Deserialize<byte[]>(jsonArray);
            });
    }
}