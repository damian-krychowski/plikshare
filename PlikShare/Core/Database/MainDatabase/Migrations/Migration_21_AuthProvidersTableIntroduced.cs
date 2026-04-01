using PlikShare.Core.SQLite;

namespace PlikShare.Core.Database.MainDatabase.Migrations;

public class Migration_21_AuthProvidersTableIntroduced : SQLiteMigrationBase
{
    public override string Name => "auth_providers_table_introduced";
    public override DateOnly Date { get; } = new(2026, 4, 1);
    public override PlikShareDbType Type { get; } = PlikShareDb.Type;

    protected override string[] ManifestResourceNames { get; } =
    [
        "PlikShare.Core.Database.MainDatabase.Migrations.21_auth_providers_table_introduced.sql"
    ];
}
