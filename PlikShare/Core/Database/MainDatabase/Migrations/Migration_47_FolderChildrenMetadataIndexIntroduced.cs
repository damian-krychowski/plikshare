using PlikShare.Core.SQLite;

namespace PlikShare.Core.Database.MainDatabase.Migrations;

/// <summary>
/// Partial index for the folder-listing children query: it contains only live, completed dependent
/// files with metadata (thumbnails, OCR artifacts), so fetching a folder's children scans exactly
/// the matching rows instead of walking the whole fi_folder_id range and rejecting parents one by
/// one. For folders without thumbnails the scan becomes empty instead of folder-sized.
/// </summary>
public class Migration_47_FolderChildrenMetadataIndexIntroduced : SQLiteMigrationBase
{
    public override string Name => "folder_children_metadata_index_introduced";
    public override DateOnly Date { get; } = new(2026, 6, 9);
    public override PlikShareDbType Type { get; } = PlikShareDb.Type;

    protected override string[] ManifestResourceNames { get; } =
    [
        "PlikShare.Core.Database.MainDatabase.Migrations.47_folder_children_metadata_index_introduced.sql"
    ];
}
