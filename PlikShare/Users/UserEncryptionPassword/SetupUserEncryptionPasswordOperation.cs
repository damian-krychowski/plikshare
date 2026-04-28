using System.Security.Cryptography;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using PlikShare.Users.Cache;
using PlikShare.Workspaces.Members.GrantEncryptionAccess;
using Serilog;

namespace PlikShare.Users.UserEncryptionPassword;

/// <summary>
/// First-time setup of a user's encryption password. Generates a fresh X25519 keypair,
/// a random recovery seed, and wraps the private key twice — once with the password-derived
/// KEK, once with the recovery-seed-derived KEK. Persists everything to u_users and returns
/// the BIP-39 recovery code for the caller to show to the user exactly once.
///
/// When the user registered with an invitation code that staged ephemeral workspace DEKs
/// (brand-new-invitee path on a full-encryption storage), the caller forwards that same
/// code here. We atomically upsert the encryption metadata AND promote each <c>ewek_*</c>
/// row to a <c>wek_*</c> wrap sealed to the just-generated public key, so the user walks
/// out of setup already-granted on every workspace they were invited to.
///
/// Rejects users who already have encryption configured — change/reset flows are separate.
/// </summary>
public class SetupUserEncryptionPasswordOperation(
    DbWriteQueue dbWriteQueue,
    UpsertUserEncryptionDataQuery upsertUserEncryptionDataQuery,
    PromoteEphemeralWorkspaceEncryptionKeysQuery promoteEphemeralWorkspaceEncryptionKeysQuery,
    NotifyOwnersOfPendingGrantsQuery notifyOwnersOfPendingGrantsQuery)
{
    public async Task<Result> Execute(
        UserContext user,
        string encryptionPassword,
        string? invitationCode,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        if (user.EncryptionMetadata is not null)
        {
            Log.Warning("User '{UserId}' already has encryption configured — setup rejected.", user.Id);
            return new Result(ResultCode.AlreadyConfigured);
        }

        using var keypair = UserKeyPair.Generate();
        var recoverySeed = UserEncryptionRecovery.GenerateRecoverySeed();

        var artifacts = await BuildSetupArtifacts(
            keypair: keypair,
            encryptionPassword: encryptionPassword,
            recoverySeed: recoverySeed);

        var hasInvitationCode = !string.IsNullOrWhiteSpace(
            invitationCode);
        
        var invitationCodeBytes = hasInvitationCode
            ? Base62Encoding.FromBase62ToBytes(invitationCode!)
            : null;

        WriteResult writeResult;
        try
        {
            writeResult = await dbWriteQueue.Execute(
                operationToEnqueue: context => ExecuteWriteOperation(
                    dbWriteContext: context,
                    userId: user.Id,
                    artifacts: artifacts,
                    invitationCodeBytes: invitationCodeBytes),
                cancellationToken: cancellationToken);
        }
        finally
        {
            if (invitationCodeBytes is not null)
                CryptographicOperations.ZeroMemory(invitationCodeBytes);
        }

        if (writeResult.Code == UpsertUserEncryptionDataQuery.ResultCode.UserNotFound)
            return new Result(ResultCode.UserNotFound);

        if (writeResult.PromotedCount > 0)
        {
            Log.Information(
                "User '{UserId}' encryption-password setup promoted {PromotedCount} ephemeral workspace key(s) using invitation code.",
                user.Id, writeResult.PromotedCount);
        }
        else if (hasInvitationCode)
        {
            Log.Information(
                "User '{UserId}' encryption-password setup received an invitation code but had no ephemeral workspace keys to promote.",
                user.Id);
        }

        // Any still-pending grants — workspaces the user was invited to post-registration, or
        // where their ephemeral wrap expired via TTL before setup — need the owner to act.
        // Promotion above has already reduced this set to exactly the deferred cases.
        await notifyOwnersOfPendingGrantsQuery.Execute(
            userId: user.Id,
            inviteeEmail: user.Email.Value,
            correlationId: correlationId,
            cancellationToken: cancellationToken);

        return new Result(
            code: ResultCode.Ok,
            privateKey: keypair.PrivateKey.Clone(),
            recoveryCode: RecoveryCodeCodec.Encode(recoverySeed));
    }

    private static async Task<EncryptionSetupArtifacts> BuildSetupArtifacts(
        UserKeyPair.KeyMaterial keypair,
        string encryptionPassword,
        byte[] recoverySeed)
    {
        var kdfSalt = EncryptionPasswordKdf.GenerateSalt();
        var kdfParams = EncryptionPasswordKdf.Params.Default;

        using var passwordKek = await EncryptionPasswordKdf.DeriveKek(
            encryptionPassword,
            kdfSalt,
            kdfParams);

        var verifyHash = passwordKek.Use(
            static kekSpan => EncryptionPasswordKdf.ComputeVerifyHash(kekSpan));

        var encryptedPrivateKey = SecureBytes.UseBoth(
            first: keypair.PrivateKey,
            second: passwordKek,
            action: static (privateKeySpan, kekSpan) => SymmetricAeadWrap.Wrap(
                kek: kekSpan, 
                plaintext: privateKeySpan));

        using var recoveryKek = UserEncryptionRecovery.DeriveRecoveryKek(
            recoverySeed);

        var recoveryWrappedPrivateKey = SecureBytes.UseBoth(
            first: keypair.PrivateKey,
            second: recoveryKek,
            action: static (privateKeySpan, kekSpan) => SymmetricAeadWrap.Wrap(kekSpan, privateKeySpan));

        var recoveryVerifyHash = UserEncryptionRecovery.ComputeVerifyHash(
            recoverySeed);

        return new EncryptionSetupArtifacts(
            PublicKey: keypair.PublicKey,
            EncryptedPrivateKey: encryptedPrivateKey,
            KdfSalt: kdfSalt,
            KdfParams: kdfParams,
            VerifyHash: verifyHash,
            RecoveryWrappedPrivateKey: recoveryWrappedPrivateKey,
            RecoveryVerifyHash: recoveryVerifyHash);
    }

    private WriteResult ExecuteWriteOperation(
        SqliteWriteContext dbWriteContext,
        int userId,
        EncryptionSetupArtifacts artifacts,
        byte[]? invitationCodeBytes)
    {
        using var transaction = dbWriteContext.Connection.BeginTransaction();

        try
        {
            var upsertCode = upsertUserEncryptionDataQuery.ExecuteTransaction(
                dbWriteContext: dbWriteContext,
                userId: userId,
                publicKey: artifacts.PublicKey,
                encryptedPrivateKey: artifacts.EncryptedPrivateKey,
                kdfSalt: artifacts.KdfSalt,
                kdfParams: artifacts.KdfParams,
                verifyHash: artifacts.VerifyHash,
                recoveryWrappedPrivateKey: artifacts.RecoveryWrappedPrivateKey,
                recoveryVerifyHash: artifacts.RecoveryVerifyHash,
                transaction: transaction);

            if (upsertCode == UpsertUserEncryptionDataQuery.ResultCode.UserNotFound)
            {
                transaction.Rollback();
                return new WriteResult(upsertCode, 0);
            }

            var promotedCount = invitationCodeBytes is null
                ? 0
                : promoteEphemeralWorkspaceEncryptionKeysQuery.ExecuteTransaction(
                    dbWriteContext: dbWriteContext,
                    userId: userId,
                    publicKey: artifacts.PublicKey,
                    invitationCodeBytes: invitationCodeBytes,
                    transaction: transaction);

            transaction.Commit();
            return new WriteResult(upsertCode, promotedCount);
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private sealed record EncryptionSetupArtifacts(
        byte[] PublicKey,
        byte[] EncryptedPrivateKey,
        byte[] KdfSalt,
        EncryptionPasswordKdf.Params KdfParams,
        byte[] VerifyHash,
        byte[] RecoveryWrappedPrivateKey,
        byte[] RecoveryVerifyHash);

    private readonly record struct WriteResult(
        UpsertUserEncryptionDataQuery.ResultCode Code,
        int PromotedCount);

    public enum ResultCode
    {
        Ok = 0,
        AlreadyConfigured,
        UserNotFound
    }

    public sealed class Result(
        ResultCode code,
        SecureBytes? privateKey = null,
        string? recoveryCode = null) : IDisposable
    {
        public ResultCode Code { get; } = code;
        public SecureBytes? PrivateKey { get; } = privateKey;
        public string? RecoveryCode { get; } = recoveryCode;

        public void Dispose() => PrivateKey?.Dispose();
    }
}
