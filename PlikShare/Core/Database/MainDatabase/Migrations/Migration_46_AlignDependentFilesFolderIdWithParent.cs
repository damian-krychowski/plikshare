using Microsoft.Data.Sqlite;
using PlikShare.Core.SQLite;

namespace PlikShare.Core.Database.MainDatabase.Migrations;

/// <summary>
/// Dependent files (thumbnails, OCR artifacts) are created with their parent's fi_folder_id, but
/// historically moving the parent to another folder and restoring it from trash did not cascade
/// the new folder onto the children — leaving them stranded in the old folder (or at NULL after
/// a restore). Re-align every live child with its parent's current folder so folder-scoped
/// operations (e.g. hard delete of a folder) treat parent and children consistently.
/// </summary>
public class Migration_46_AlignDependentFilesFolderIdWithParent : ISQLiteMigration
{
    public string Name => "align_dependent_files_folder_id_with_parent";
    public DateOnly Date { get; } = new(2026, 6, 9);
    public PlikShareDbType Type { get; } = PlikShareDb.Type;

    public void Run(SqliteConnection connection, SqliteTransaction transaction)
    {
        connection
            .NonQueryCmd(
                sql: """
                     UPDATE fi_files
                     SET fi_folder_id = (
                         SELECT parent.fi_folder_id
                         FROM fi_files AS parent
                         WHERE parent.fi_id = fi_files.fi_parent_file_id
                     )
                     WHERE
                         fi_parent_file_id IS NOT NULL
                         AND fi_deleted_at IS NULL
                         AND fi_folder_id IS NOT (
                             SELECT parent.fi_folder_id
                             FROM fi_files AS parent
                             WHERE parent.fi_id = fi_files.fi_parent_file_id
                         )
                     """,
                transaction: transaction)
            .Execute();
    }
}
