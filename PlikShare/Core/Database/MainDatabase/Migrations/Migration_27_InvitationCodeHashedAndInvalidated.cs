using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using PlikShare.Core.SQLite;

namespace PlikShare.Core.Database.MainDatabase.Migrations;

/// <summary>
/// Replaces the plaintext <c>u_invitation_code</c> column with <c>u_invitation_code_hash</c>
/// (SHA-256 of the plaintext). Existing invitation rows keep their codes alive by running
/// each plaintext through SHA-256 during the migration, so previously emailed invitation
/// links still work.
/// </summary>
public class Migration_27_InvitationCodeHashedAndInvalidated : ISQLiteMigration
{
    public string Name => "invitation_code_hashed";
    public DateOnly Date { get; } = new(2026, 4, 19);
    public PlikShareDbType Type { get; } = PlikShareDb.Type;

    public void Run(SqliteConnection connection, SqliteTransaction transaction)
    {
        connection.CreateFunction("app_hash_invitation_code",
            (string? code) => code is null ? null : SHA256.HashData(Encoding.UTF8.GetBytes(code)));

        connection.NonQueryCmd(
            sql: "ALTER TABLE u_users ADD COLUMN u_invitation_code_hash BLOB NULL",
            transaction: transaction)
            .Execute();

        connection.NonQueryCmd(
            sql: """
                 UPDATE u_users
                 SET u_invitation_code_hash = app_hash_invitation_code(u_invitation_code)
                 WHERE u_invitation_code IS NOT NULL
                 """,
            transaction: transaction)
            .Execute();

        connection.NonQueryCmd(
            sql: "DROP INDEX IF EXISTS unique__u_users__u_invitation_code",
            transaction: transaction)
            .Execute();

        connection.NonQueryCmd(
            sql: "ALTER TABLE u_users DROP COLUMN u_invitation_code",
            transaction: transaction)
            .Execute();

        connection.NonQueryCmd(
            sql: "CREATE UNIQUE INDEX IF NOT EXISTS unique__u_users__u_invitation_code_hash ON u_users (u_invitation_code_hash)",
            transaction: transaction)
            .Execute();
    }
}
