using PlikShare.Core.SQLite;

namespace PlikShare.Core.Database.MainDatabase.Migrations;

public class Migration_16_SignUpCheckboxesIntroduced : SQLiteMigrationBase
{
    public override string Name => "sign_up_checkboxes_introduced";
    public override DateOnly Date { get; } = new(2025, 3, 17);
    public override PlikShareDbType Type { get; } = PlikShareDb.Type;

    protected override string[] ManifestResourceNames { get; } =
    [
        "PlikShare.Core.Database.MainDatabase.Migrations.16_sign_up_checkboxes_introduced.sql"
    ];
}