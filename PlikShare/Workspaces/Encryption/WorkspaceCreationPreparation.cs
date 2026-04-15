using System.Security.Cryptography;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.Id;
using PlikShare.Users.UserEncryptionPassword;
using Serilog;

namespace PlikShare.Workspaces.Encryption;

/// <summary>
/// Pre-flight step for workspace creation. Runs entirely on the caller's thread, ahead of
/// any DB write queue, and does two things:
/// 1. Resolves the target storage's id + encryption type (non-transactional read).
/// 2. For full-encrypted storages, builds the crypto artifacts needed for the new workspace:
///    the per-workspace salt and the owner's sealed-box wrap of the freshly-derived
///    Workspace DEK.
///
/// Every plaintext buffer it touches — the caller's X25519 private key, the unsealed
/// Storage DEK, the derived Workspace DEK — is zeroed in a <c>finally</c>. The artifacts
/// it returns are inert ciphertext, safe to cross thread boundaries into a background
/// writer.
///
/// Kept separate from <c>CreateWorkspaceQuery</c> so the write-side query stays reduced to
/// MaxWorkspaceNumber check + INSERTs, and so callers like <c>CreateIntegrationWithWorkspaceQuery</c>
/// that only need the storage-type check can use <see cref="ResolveStorageContextForRead"/>
/// directly without dragging in crypto state.
/// </summary>
public class WorkspaceCreationPreparation(
    PlikShareDb plikShareDb,
    UserEncryptionDataReader userEncryptionDataReader,
    StorageEncryptionKeyReader storageEncryptionKeyReader)
{
    public Result Prepare(
        StorageExtId storageExternalId,
        int ownerId,
        Func<byte[]?> loadUserPrivateKey)
    {
        var storageContext = ResolveStorageContextForRead(storageExternalId);
        if (storageContext is null)
            return new Result(Code: ResultCode.StorageNotFound);

        // None/Managed storages need no pre-flight artifacts — the query inserts the
        // workspace row with NULL encryption salt and no wek row.
        if (storageContext.Value.EncryptionType != StorageEncryptionType.Full)
            return new Result(Code: ResultCode.Ok);

        return PrepareFullEncryptionArtifacts(
            ownerId: ownerId,
            storageId: storageContext.Value.Id,
            loadUserPrivateKey: loadUserPrivateKey);
    }

    /// <summary>
    /// Non-transactional read of a storage row's id + encryption type. Safe to call before
    /// a transaction opens — storage type is effectively immutable, and if the storage row
    /// disappears between this read and a subsequent workspace INSERT, the FK constraint
    /// on <c>w_workspaces.w_storage_id</c> will fail and the caller translates it back to
    /// <c>StorageNotFound</c>.
    /// </summary>
    public StorageContext? ResolveStorageContextForRead(StorageExtId storageExternalId)
    {
        using var connection = plikShareDb.OpenConnection();

        var (isEmpty, ctx) = connection
            .OneRowCmd(
                sql: """
                     SELECT s_id, s_encryption_type
                     FROM s_storages
                     WHERE s_external_id = $storageExternalId
                     LIMIT 1
                     """,
                readRowFunc: reader => new StorageContext(
                    Id: reader.GetInt32(0),
                    EncryptionType: StorageEncryptionExtensions.FromDbValue(reader.GetStringOrNull(1))))
            .WithParameter("$storageExternalId", storageExternalId.Value)
            .Execute();

        return isEmpty ? null : ctx;
    }

    private Result PrepareFullEncryptionArtifacts(
        int ownerId,
        int storageId,
        Func<byte[]?> loadUserPrivateKey)
    {
        var userPrivateKey = loadUserPrivateKey();
        if (userPrivateKey is null)
            return new Result(Code: ResultCode.UserEncryptionSessionRequired);

        try
        {
            var ownerEncryption = userEncryptionDataReader.LoadForUser(
                userId: ownerId);

            if (ownerEncryption is null)
                return new Result(Code: ResultCode.CreatorEncryptionNotSetUp);

            var wrappedStorageDek = storageEncryptionKeyReader.TryLoadWrappedDek(
                storageId: storageId,
                userId: ownerId);

            if (wrappedStorageDek is null)
                return new Result(Code: ResultCode.NotAStorageAdmin);

            byte[] storageDek;
            try
            {
                storageDek = UserKeyPair.OpenSealed(
                    recipientPrivateKey: userPrivateKey,
                    sealed_: wrappedStorageDek);
            }
            catch (Exception e)
            {
                // Opaque failure — corrupted wrap, mismatched key, tamper. Return the same
                // code as a missing wrap so the caller's UX stays consistent.
                Log.Error(e,
                    "Unsealing wrapped Storage DEK failed for User#{UserId} while preparing a workspace on Storage#{StorageId}.",
                    ownerId, storageId);

                return new Result(Code: ResultCode.NotAStorageAdmin);
            }

            try
            {
                var salt = new byte[WorkspaceDekDerivation.SaltSize];
                RandomNumberGenerator.Fill(salt);

                var workspaceDek = WorkspaceDekDerivation.Derive(
                    storageDek: storageDek,
                    workspaceSalt: salt);

                try
                {
                    var wrapped = UserKeyPair.SealTo(
                        recipientPublicKey: ownerEncryption.PublicKey,
                        plaintext: workspaceDek);

                    return new Result(
                        Code: ResultCode.Ok,
                        Artifacts: new WorkspaceFullEncryptionArtifacts(
                            EncryptionSalt: salt,
                            OwnerWrappedWorkspaceDek: wrapped));
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(workspaceDek);
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(storageDek);
            }
        }
        finally
        {
            // The private-key buffer was allocated by the factory delegate specifically for
            // this call and has no other owner, so it's safe to wipe synchronously.
            CryptographicOperations.ZeroMemory(userPrivateKey);
        }
    }

    public readonly record struct StorageContext(
        int Id,
        StorageEncryptionType EncryptionType);

    public enum ResultCode
    {
        Ok = 0,
        StorageNotFound,
        UserEncryptionSessionRequired,
        CreatorEncryptionNotSetUp,
        NotAStorageAdmin
    }

    public readonly record struct Result(
        ResultCode Code,
        WorkspaceFullEncryptionArtifacts? Artifacts = null);
}

/// <summary>
/// Pre-computed crypto artifacts that must be inserted alongside a full-encrypted workspace
/// row. Both fields are mandatory and always co-present — either the caller has them (Full
/// storage) or they have none (None/Managed). Passed as a single nullable value through the
/// workspace creation pipeline so the two pieces can never drift out of sync.
/// </summary>
public sealed record WorkspaceFullEncryptionArtifacts(
    byte[] EncryptionSalt,
    byte[] OwnerWrappedWorkspaceDek);
