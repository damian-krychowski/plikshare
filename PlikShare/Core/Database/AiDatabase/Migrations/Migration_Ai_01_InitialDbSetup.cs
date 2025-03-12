using PlikShare.Core.Database.AiDatabase;
using PlikShare.Core.SQLite;

namespace PlikShare.Core.Database.MainDatabase.Migrations;

public class Migration_Ai_01_InitialDbSetup: SQLiteMigrationBase
{
    public override string Name => "initial_ai_db_setup";
    public override DateOnly Date { get; } = new(2025, 2, 23);
    public override PlikShareDbType Type { get; } = PlikShareAiDb.Type;

    protected override string[] ManifestResourceNames { get; } =
    [
        "PlikShare.Core.Database.AiDatabase.Migrations.01_initial_ai_db_structure_migration.sql"
    ];
}