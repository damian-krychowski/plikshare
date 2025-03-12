using Microsoft.Data.Sqlite;
using PlikShare.Core.DataProtection;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;

namespace PlikShare.Core.Database.MainDatabase.Migrations;

public class Migration_15_ReencryptDatabaseFromAesCcmToAesGcm(MasterEncryptionKeyProvider masterEncryptionKeyProvider) : ISQLiteMigration
{
    public string Name => "reencrypt_database_from_aes_ccm_to_aes_gcm";
    public DateOnly Date { get; } = new(2025, 3, 2);
    public PlikShareDbType Type { get; } = PlikShareDb.Type;
    
    public void Run(SqliteConnection connection, SqliteTransaction transaction)
    {
        var aesCcm = new AesCcmMasterDataEncryption(masterEncryptionKeyProvider);
        var aesGcm = new AesGcmMasterDataEncryption(masterEncryptionKeyProvider);

        connection.CreateFunction("app_reencrypt_from_ccm_to_gcm",
            (byte[]? bytes) =>
            {
                if (bytes is null)
                    return null;

                var decrypted = aesCcm.Decrypt(bytes);
                var reencrypted = aesGcm.Encrypt(decrypted);

                return reencrypted;
            });

        connection.CreateFunction("app_reencrypt_from_ccm_to_gcm_base64",
            (string? encrypted) =>
            {
                if (encrypted is null)
                    return null;

                var decrypted = aesCcm.DecryptFromBase64(encrypted);
                var reencrypted = aesGcm.EncryptToBase64(decrypted);

                return reencrypted;
            });

        var storageIds = connection
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

        var emailProviderIds = connection
            .Cmd(
                sql: """
                     UPDATE ep_email_providers
                     SET ep_details_encrypted = app_reencrypt_from_ccm_to_gcm(ep_details_encrypted)
                     RETURNING ep_id
                     """,
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .Execute();

        var integrationIds = connection
            .Cmd(
                sql: """
                     UPDATE i_integrations
                     SET i_details_encrypted = app_reencrypt_from_ccm_to_gcm(i_details_encrypted)
                     RETURNING i_id
                     """,
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .Execute();
        
        var appSettings = connection
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

        var deletedAppSettings = connection
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