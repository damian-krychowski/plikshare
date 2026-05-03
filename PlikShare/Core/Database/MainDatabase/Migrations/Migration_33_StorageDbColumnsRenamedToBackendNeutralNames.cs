using Microsoft.Data.Sqlite;
using PlikShare.Core.SQLite;

namespace PlikShare.Core.Database.MainDatabase.Migrations;

/// <summary>
/// Renames the three storage-touching columns from S3-flavoured names to
/// backend-neutral ones:
/// <list type="bullet">
///   <item><c>fi_files.fi_s3_key_secret_part</c> → <c>fi_key_secret_part</c></item>
///   <item><c>fu_file_uploads.fu_file_s3_key_secret_part</c> → <c>fu_file_key_secret_part</c></item>
///   <item><c>fu_file_uploads.fu_s3_upload_id</c> → <c>fu_multipart_upload_id</c></item>
/// </list>
/// </summary>
public class Migration_33_StorageDbColumnsRenamedToBackendNeutralNames : ISQLiteMigration
{
    public string Name => "storage_db_columns_renamed_to_backend_neutral_names";
    public DateOnly Date { get; } = new(2026, 5, 4);
    public PlikShareDbType Type { get; } = PlikShareDb.Type;

    public void Run(SqliteConnection connection, SqliteTransaction transaction)
    {
        connection
            .NonQueryCmd(
                sql: "ALTER TABLE fi_files RENAME COLUMN fi_s3_key_secret_part TO fi_key_secret_part",
                transaction: transaction)
            .Execute();

        connection
            .NonQueryCmd(
                sql: "ALTER TABLE fu_file_uploads RENAME COLUMN fu_file_s3_key_secret_part TO fu_file_key_secret_part",
                transaction: transaction)
            .Execute();

        connection
            .NonQueryCmd(
                sql: "ALTER TABLE fu_file_uploads RENAME COLUMN fu_s3_upload_id TO fu_multipart_upload_id",
                transaction: transaction)
            .Execute();
    }
}
