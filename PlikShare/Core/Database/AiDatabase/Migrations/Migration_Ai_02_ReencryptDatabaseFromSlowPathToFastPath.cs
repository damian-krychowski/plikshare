using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using PlikShare.Core.Database.AiDatabase;
using PlikShare.Core.Database.Migrations.Legacy;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;

namespace PlikShare.Core.Database.MainDatabase.Migrations;

/// <summary>
/// Re-encrypts AI-message rows from slow-path AES-GCM (per-call PBKDF2 / per-conversation derived
/// key) to fast-path AES-GCM (process-wide stretched master key). Mirrors Migration_34 but
/// targets the AI database. The slow-path decoder lives in <see cref="LegacyAesGcm"/>.
/// </summary>
public class Migration_Ai_02_ReencryptDatabaseFromSlowPathToFastPath(
    MasterEncryptionKeyProvider masterEncryptionKeyProvider,
    IMasterDataEncryption masterDataEncryption) : ISQLiteMigration
{
    public string Name => "ai_reencrypt_database_from_slow_path_to_fast_path";
    public DateOnly Date { get; } = new(2026, 5, 6);
    public PlikShareDbType Type { get; } = PlikShareAiDb.Type;

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
                     UPDATE aim_ai_messages
                     SET
                        aim_message_encrypted = app_reencrypt_slow_to_fast(aim_message_encrypted),
                        aim_includes_encrypted = app_reencrypt_slow_to_fast(aim_includes_encrypted)
                     RETURNING aim_id
                     """,
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .Execute();
    }
}
