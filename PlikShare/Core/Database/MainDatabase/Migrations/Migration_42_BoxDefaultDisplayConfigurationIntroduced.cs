using PlikShare.Core.SQLite;

namespace PlikShare.Core.Database.MainDatabase.Migrations;

public class Migration_42_BoxDefaultDisplayConfigurationIntroduced : SQLiteMigrationBase
{
    public override string Name => "box_default_display_configuration_introduced";
    public override DateOnly Date { get; } = new(2026, 6, 3);
    public override PlikShareDbType Type { get; } = PlikShareDb.Type;

    protected override string[] ManifestResourceNames { get; } =
    [
        "PlikShare.Core.Database.MainDatabase.Migrations.42_box_default_display_configuration_introduced.sql"
    ];
}
