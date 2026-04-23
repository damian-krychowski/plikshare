using Microsoft.Data.Sqlite;
using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using Serilog;

namespace PlikShare.Workspaces.Members.GrantEncryptionAccess;

/// <summary>
/// Resolves the shared ephemeral user keypair in <c>euek_ephemeral_user_encryption_keys</c>
/// for a brand-new invitee and returns its public key. Three outcomes:
///
/// 1. Keypair already exists → returns the stored public key. The invitation code is
///    ignored (pass null if you don't have it) — subsequent owners inviting the same
///    pending user re-use the shared public key without touching the code.
/// 2. Keypair does not exist AND an invitation code was provided → generates a fresh
///    X25519 keypair, wraps its private key with a KEK derived from the invitation code
///    via <see cref="InvitationCodePrivateKeyWrap"/>, inserts the euek row, returns the
///    new public key.
/// 3. Keypair does not exist AND no invitation code → logs a warning and returns null.
///    The only way to produce the private-key wrap is the code that the invitee received
///    by email; without it the ephemeral path is not reachable for this user on this
///    invite. The caller drops the invite into the deferred-grant path: membership row
///    is created, no ewek is staged, owner must grant access manually after the invitee
///    sets up their encryption password (via <see cref="NotifyOwnersOfPendingGrantsQuery"/>).
///
/// This is the decoupling that makes multi-workspace invitations of pending invitees work:
/// the invitation code is only needed when the keypair is first created and again at
/// encryption password setup (for <see cref="PromoteEphemeralWorkspaceEncryptionKeysQuery"/>);
/// everyone in between works with the plaintext public key.
///
/// Follows the project's get-or-create idiom: read first, then INSERT ON CONFLICT DO NOTHING
/// RETURNING, then fallback SELECT if the insert lost a race. SQLite enforces the uniqueness
/// invariant directly — we don't rely on single-writer ordering.
/// </summary>
public class CreateOrGetEphemeralUserKeyPairQuery(IClock clock)
{
    public EphemeralUserPublicKey? ExecuteTransaction(
        SqliteWriteContext dbWriteContext,
        int userId,
        byte[]? invitationCodeBytes,
        int? createdByUserId,
        SqliteTransaction transaction)
    {
        var existing = TrySelectPublicKey(
            dbWriteContext: dbWriteContext, 
            userId: userId, 
            transaction: transaction);

        if (!existing.IsEmpty)
            return new EphemeralUserPublicKey(existing.Value);

        if (invitationCodeBytes is null)
        {
            Log.Warning(
                "Cannot stage ephemeral user keypair for User#{UserId}: no euek row exists and " +
                "no invitation code was provided. The caller should fall back to the deferred " +
                "grant path.",
                userId);

            return null;
        }

        using var keypair = UserKeyPair.Generate();

        var encryptedPrivateKey = keypair.PrivateKey.Use(
            state: new InvitationCodeEntropy(invitationCodeBytes),
            action: static (privateKeySpan, entropy) =>
                InvitationCodePrivateKeyWrap.Wrap(entropy.Bytes, privateKeySpan));

        var inserted = TryInsertEphemeralKeyPair(
            dbWriteContext: dbWriteContext,
            userId: userId,
            publicKey: keypair.PublicKey,
            encryptedPrivateKey: encryptedPrivateKey,
            createdByUserId: createdByUserId,
            transaction: transaction);

        if (!inserted.IsEmpty)
        {
            Log.Information(
                "Ephemeral user keypair was created for User#{UserId} (by User#{CreatedByUserId}).",
                userId, createdByUserId);

            return new EphemeralUserPublicKey(inserted.Value);
        }

        var raced = TrySelectPublicKey(dbWriteContext, userId, transaction);
        if (raced.IsEmpty)
        {
            throw new InvalidOperationException(
                $"Cannot create nor select ephemeral user keypair for User#{userId}.");
        }

        return new EphemeralUserPublicKey(raced.Value);
    }

    private static SQLiteOneRowCommandResult<byte[]> TrySelectPublicKey(
        SqliteWriteContext dbWriteContext,
        int userId,
        SqliteTransaction transaction)
    {
        return dbWriteContext
            .OneRowCmd(
                sql: """
                     SELECT euek_public_key
                     FROM euek_ephemeral_user_encryption_keys
                     WHERE euek_user_id = $userId
                     """,
                readRowFunc: reader => reader.GetFieldValue<byte[]>(0),
                transaction: transaction)
            .WithParameter("$userId", userId)
            .Execute();
    }

    private SQLiteOneRowCommandResult<byte[]> TryInsertEphemeralKeyPair(
        SqliteWriteContext dbWriteContext,
        int userId,
        byte[] publicKey,
        byte[] encryptedPrivateKey,
        int? createdByUserId,
        SqliteTransaction transaction)
    {
        return dbWriteContext
            .OneRowCmd(
                sql: """
                     INSERT INTO euek_ephemeral_user_encryption_keys(
                         euek_user_id,
                         euek_public_key,
                         euek_encrypted_private_key,
                         euek_created_at,
                         euek_created_by_user_id
                     ) VALUES (
                         $userId,
                         $publicKey,
                         $encryptedPrivateKey,
                         $createdAt,
                         $createdByUserId
                     )
                     ON CONFLICT(euek_user_id) DO NOTHING
                     RETURNING euek_public_key
                     """,
                readRowFunc: reader => reader.GetFieldValue<byte[]>(0),
                transaction: transaction)
            .WithParameter("$userId", userId)
            .WithParameter("$publicKey", publicKey)
            .WithParameter("$encryptedPrivateKey", encryptedPrivateKey)
            .WithParameter("$createdAt", clock.UtcNow)
            .WithParameter("$createdByUserId", (object?)createdByUserId ?? DBNull.Value)
            .Execute();
    }

    private readonly record struct InvitationCodeEntropy(byte[] Bytes);
}

public readonly record struct EphemeralUserPublicKey(byte[] Bytes);
