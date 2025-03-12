using PlikShare.Core.Database;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;

namespace PlikShare.Core.Database.MainDatabase.Migrations;

public class Migration_01_InitialDbSetup: SQLiteMigrationBase
{
    public override string Name => "initial_db_setup";
    public override DateOnly Date { get; } = new(2024, 6, 7);
    public override PlikShareDbType Type { get; } = PlikShareDb.Type;

    protected override string[] ManifestResourceNames { get; } =
    [
        "PlikShare.Core.Database.MainDatabase.Migrations.01_initial_db_structure_migration.sql"
    ];
}