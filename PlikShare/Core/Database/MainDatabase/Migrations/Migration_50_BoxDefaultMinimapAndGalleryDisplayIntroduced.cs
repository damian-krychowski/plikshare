using PlikShare.Core.SQLite;

namespace PlikShare.Core.Database.MainDatabase.Migrations;

public class Migration_50_BoxDefaultMinimapAndGalleryDisplayIntroduced : SQLiteMigrationBase
{
    public override string Name => "box_default_minimap_and_gallery_display_introduced";
    public override DateOnly Date { get; } = new(2026, 6, 12);
    public override PlikShareDbType Type { get; } = PlikShareDb.Type;

    protected override string[] ManifestResourceNames { get; } =
    [
        "PlikShare.Core.Database.MainDatabase.Migrations.50_box_default_minimap_and_gallery_display_introduced.sql"
    ];
}
