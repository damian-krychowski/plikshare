using PlikShare.Core.SQLite;

namespace PlikShare.Core.Database.MainDatabase.Migrations;

public class Migration_13_FilesAndFoldersLookupIndicesIntroduced : SQLiteMigrationBase
{
    public override string Name => "files_and_folders_lookup_indices_introduced";
    public override DateOnly Date { get; } = new(2025, 2, 8);
    public override PlikShareDbType Type { get; } = PlikShareDb.Type;

    protected override string[] ManifestResourceNames { get; } =
    [
        "PlikShare.Core.Database.MainDatabase.Migrations.13_files_and_folders_lookup_indices_introduced.sql"
    ];
}