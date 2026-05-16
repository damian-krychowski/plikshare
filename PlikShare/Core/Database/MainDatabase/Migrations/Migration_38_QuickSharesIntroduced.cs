using PlikShare.Core.SQLite;

namespace PlikShare.Core.Database.MainDatabase.Migrations;

public class Migration_38_QuickSharesIntroduced : SQLiteMigrationBase
{
    public override string Name => "quick_shares_introduced";
    public override DateOnly Date { get; } = new(2026, 5, 16);
    public override PlikShareDbType Type { get; } = PlikShareDb.Type;

    protected override string[] ManifestResourceNames { get; } =
    [
        "PlikShare.Core.Database.MainDatabase.Migrations.38_quick_shares_introduced.sql"
    ];
}
