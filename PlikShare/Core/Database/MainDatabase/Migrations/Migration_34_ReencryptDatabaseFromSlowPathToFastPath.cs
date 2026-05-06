using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using PlikShare.Core.Database.Migrations.Legacy;
using PlikShare.Core.DataProtection;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;

namespace PlikShare.Core.Database.MainDatabase.Migrations;

/// <summary>
/// Re-encrypts existing rows from the slow-path AES-GCM format (per-call PBKDF2 with 650 000
/// iterations and a random salt per record) to the fast-path AES-GCM format (process-wide
/// stretched master key derived once at startup). The slow-path decoder lives in
/// <see cref="LegacyAesGcm"/> — this migration only orchestrates SQL UDFs and table updates.
///
/// Touches BLOB columns: storage details (×2), auth client secrets, email-provider details,
/// integration details. ASP.NET DataProtection keys (<see cref="SQLiteDataProtectionRepository.AppSettingsPrefix"/>)
/// are deleted instead of re-encrypted — the framework regenerates them on next startup.
/// Active sessions / antiforgery tokens are invalidated once at upgrade, which mirrors what
/// Migration_15 already did for the same keys.
/// </summary>
public class Migration_34_ReencryptDatabaseFromSlowPathToFastPath(
    MasterEncryptionKeyProvider masterEncryptionKeyProvider,
    IMasterDataEncryption masterDataEncryption) : ISQLiteMigration
{
    public string Name => "reencrypt_database_from_slow_path_to_fast_path";
    public DateOnly Date { get; } = new(2026, 5, 6);
    public PlikShareDbType Type { get; } = PlikShareDb.Type;

    public void Run(SqliteConnection connection, SqliteTransaction transaction)
    {
        connection.CreateFunction("app_reencrypt_slow_to_fast",
            (byte[]? bytes) =>
            {
                if (bytes is null)
                    return null;

                var plaintext = LegacyAesGcm.Decrypt(bytes, masterEncryptionKeyProvider);

                try
                {
                    return masterDataEncryption.EncryptBytes(plaintext);
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(plaintext);
                }
            });

        connection
            .Cmd(
                sql: """
                     UPDATE s_storages
                     SET
                        s_details_encrypted = app_reencrypt_slow_to_fast(s_details_encrypted),
                        s_encryption_details_encrypted = app_reencrypt_slow_to_fast(s_encryption_details_encrypted)
                     RETURNING s_id
                     """,
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .Execute();

        connection
            .Cmd(
                sql: """
                     UPDATE ap_auth_providers
                     SET ap_client_secret_encrypted = app_reencrypt_slow_to_fast(ap_client_secret_encrypted)
                     RETURNING ap_id
                     """,
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .Execute();

        connection
            .Cmd(
                sql: """
                     UPDATE ep_email_providers
                     SET ep_details_encrypted = app_reencrypt_slow_to_fast(ep_details_encrypted)
                     RETURNING ep_id
                     """,
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .Execute();

        connection
            .Cmd(
                sql: """
                     UPDATE i_integrations
                     SET i_details_encrypted = app_reencrypt_slow_to_fast(i_details_encrypted)
                     RETURNING i_id
                     """,
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .Execute();

        connection
            .Cmd(
                sql: $"""
                      DELETE FROM as_app_settings
                      WHERE as_key LIKE '{SQLiteDataProtectionRepository.AppSettingsPrefix}%'
                      RETURNING as_key
                      """,
                readRowFunc: reader => reader.GetString(0),
                transaction: transaction)
            .Execute();
    }
}
