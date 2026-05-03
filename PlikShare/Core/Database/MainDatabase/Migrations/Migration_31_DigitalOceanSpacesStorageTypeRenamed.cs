using Microsoft.Data.Sqlite;
using PlikShare.Core.SQLite;

namespace PlikShare.Core.Database.MainDatabase.Migrations;

public class Migration_31_DigitalOceanSpacesStorageTypeRenamed : ISQLiteMigration
{
    public string Name => "digital_ocean_spaces_storage_type_renamed";
    public DateOnly Date { get; } = new(2026, 5, 2);
    public PlikShareDbType Type { get; } = PlikShareDb.Type;

    public void Run(SqliteConnection connection, SqliteTransaction transaction)
    {
        connection.NonQueryCmd(
            sql: """
                 UPDATE s_storages
                 SET s_type = 'digital-ocean-spaces'
                 WHERE s_type = 'digitalocean-spaces'
                 """,
            transaction: transaction)
            .Execute();
    }
}
