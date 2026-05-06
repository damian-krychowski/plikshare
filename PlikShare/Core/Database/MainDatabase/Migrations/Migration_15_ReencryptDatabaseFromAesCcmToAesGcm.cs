using Microsoft.Data.Sqlite;
using PlikShare.Core.Database.Migrations.Legacy;
using PlikShare.Core.DataProtection;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;

namespace PlikShare.Core.Database.MainDatabase.Migrations;

/// <summary>
/// Re-encrypts existing rows from the legacy AES-CCM format to the slow-path AES-GCM format.
/// Both ciphers live in <see cref="LegacyAesCcm"/> and <see cref="LegacyAesGcm"/> — this migration
/// only orchestrates the SQL UDFs and the table updates. A later migration (34) carries these
/// rows from slow-path GCM to fast-path GCM.
/// </summary>
public class Migration_15_ReencryptDatabaseFromAesCcmToAesGcm(MasterEncryptionKeyProvider masterEncryptionKeyProvider) : ISQLiteMigration
{
    public string Name => "reencrypt_database_from_aes_ccm_to_aes_gcm";
    public DateOnly Date { get; } = new(2025, 3, 2);
    public PlikShareDbType Type { get; } = PlikShareDb.Type;

    public void Run(SqliteConnection connection, SqliteTransaction transaction)
    {
        connection.CreateFunction("app_reencrypt_from_ccm_to_gcm",
            (byte[]? bytes) =>
            {
                if (bytes is null)
                    return null;

                var decrypted = LegacyAesCcm.Decrypt(bytes, masterEncryptionKeyProvider);
                return LegacyAesGcm.Encrypt(decrypted, masterEncryptionKeyProvider);
            });

        connection.CreateFunction("app_reencrypt_from_ccm_to_gcm_base64",
            (string? encrypted) =>
            {
                if (encrypted is null)
                    return null;

                var decryptedBytes = Convert.FromBase64String(encrypted);
                var decrypted = LegacyAesCcm.Decrypt(decryptedBytes, masterEncryptionKeyProvider);
                return Convert.ToBase64String(
                    LegacyAesGcm.Encrypt(decrypted, masterEncryptionKeyProvider));
            });

        connection
            .Cmd(
                sql: """
                     UPDATE s_storages
                     SET
                        s_details_encrypted = app_reencrypt_from_ccm_to_gcm(s_details_encrypted),
                        s_encryption_details_encrypted = app_reencrypt_from_ccm_to_gcm(s_encryption_details_encrypted)
                     RETURNING s_id
                     """,
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .Execute();

        connection
            .Cmd(
                sql: """
                     UPDATE ep_email_providers
                     SET ep_details_encrypted = app_reencrypt_from_ccm_to_gcm(ep_details_encrypted)
                     RETURNING ep_id
                     """,
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .Execute();

        connection
            .Cmd(
                sql: """
                     UPDATE i_integrations
                     SET i_details_encrypted = app_reencrypt_from_ccm_to_gcm(i_details_encrypted)
                     RETURNING i_id
                     """,
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .Execute();

        connection
            .Cmd(
                sql: """
                     UPDATE as_app_settings
                     SET as_value = app_reencrypt_from_ccm_to_gcm_base64(as_value)
                     WHERE as_key IN (
                        SELECT value FROM json_each($keys)
                     )
                     RETURNING as_key
                     """,
                readRowFunc: reader => reader.GetString(0),
                transaction: transaction)
            .WithJsonParameter("$keys", new string[]
            {
                "plikshare-license"
            })
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
