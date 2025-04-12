using PlikShare.Core.SQLite;

namespace PlikShare.Core.Database.MainDatabase.Migrations;

public class Migration_20_WidgetOriginsColumnAddedToBoxLinksTable : SQLiteMigrationBase
{
    public override string Name => "widget_origins_column_added_to_box_links_table";
    public override DateOnly Date { get; } = new(2025, 4, 10);
    public override PlikShareDbType Type { get; } = PlikShareDb.Type;

    protected override string[] ManifestResourceNames { get; } =
    [
        "PlikShare.Core.Database.MainDatabase.Migrations.20_widget_origins_column_added_to_box_links_table.sql"
    ];
}