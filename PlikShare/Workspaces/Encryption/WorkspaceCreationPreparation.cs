using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using PlikShare.Storages;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.Id;
using PlikShare.Users.Cache;
using Serilog;
using System.Security.Cryptography;

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
    StorageClientStore storageClientStore)
{
    public Result Prepare(
        StorageExtId storageExternalId,
        UserContext owner,
        Func<SecureBytes?> loadUserPrivateKey)
    {
        var hasStorage = storageClientStore.TryGetClient(
            externalId: storageExternalId,
            client: out var storage);

        if (!hasStorage)
            return new Result(Code: ResultCode.StorageNotFound);
        
        // Defense-in-depth: even if the dashboard storage picker filters the list, the
        // creation endpoint must independently enforce the user's storage-access policy.
        // App-owners bypass it (CanAccessStorage handles that).
        if (!owner.CanAccessStorage(storage.StorageId))
            return new Result(Code: ResultCode.StorageNotAllowedForUser);

        // None/Managed storages need no pre-flight artifacts — the query inserts the
        // workspace row with NULL encryption salt and no wek row.
        if (storage.EncryptionType != StorageEncryptionType.Full)
        {
            return new Result(
                Code: ResultCode.Ok,
                Details: new WorkspaceDetails
                {
                    Artifacts = null,
                    Storage = storage
                });
        }

        return PrepareFullEncryptionArtifacts(
            owner: owner,
            storage: storage,
            loadUserPrivateKey: loadUserPrivateKey);
    }

    private Result PrepareFullEncryptionArtifacts(
        UserContext owner,
        IStorageClient storage,
        Func<SecureBytes?> loadUserPrivateKey)
    {
        using var userPrivateKey = loadUserPrivateKey();

        if (userPrivateKey is null)
            return new Result(Code: ResultCode.UserEncryptionSessionRequired);

        if (owner.EncryptionMetadata is null)
            return new Result(Code: ResultCode.CreatorEncryptionNotSetUp);

        // New workspaces are always derived from the storage's latest DEK version — the
        // one the creator gets wrapped for here. Older versions exist only to keep files
        // written before a past rotation decryptable.
        var latestWrappedStorageDek = owner.TryGetLatestStorageDek(
            storageId: storage.StorageId);

        if (latestWrappedStorageDek is null)
            return new Result(Code: ResultCode.NotAStorageAdmin);

        try
        {
            using var storageDekEntry = latestWrappedStorageDek.Unseal(
                userPrivateKey);

            var salt = RandomNumberGenerator.GetBytes(
                KeyDerivationChain.StepSaltSize);

            using var workspaceDek = storageDekEntry.DeriveWorkspaceDek(
                    workspaceDekSalts: [salt]);
                
            var wrapped = workspaceDek.Dek.Use(span => UserKeyPair.SealTo(
                recipientPublicKey: owner.EncryptionMetadata.PublicKey,
                plaintext: span));

            return new Result(
                Code: ResultCode.Ok,
                Details: new WorkspaceDetails(
                    Artifacts: new WorkspaceFullEncryptionArtifacts(
                        StorageDekVersion: latestWrappedStorageDek.StorageDekVersion,
                        EncryptionSalt: salt,
                        OwnerWrappedWorkspaceDek: wrapped),
                    Storage: storage));
        }
        catch (StorageDekUnsealException e)
        {
            // Opaque failure — corrupted wrap, mismatched key, tamper. Return the same
            // code as a missing wrap so the caller's UX stays consistent.
            Log.Error(e,
                "Unsealing wrapped Storage DEK v{StorageDekVersion} failed for User#{UserId} while preparing a workspace on Storage#{StorageId}.",
                e.StorageDekVersion, owner, e.StorageId);

            return new Result(Code: ResultCode.NotAStorageAdmin);
        }
    }

    public enum ResultCode
    {
        Ok = 0,
        StorageNotFound,
        UserEncryptionSessionRequired,
        CreatorEncryptionNotSetUp,
        NotAStorageAdmin,
        StorageNotAllowedForUser
    }

    public readonly record struct Result(
        ResultCode Code,
        WorkspaceDetails? Details = null);

    public readonly record struct WorkspaceDetails(
        WorkspaceFullEncryptionArtifacts? Artifacts,
        IStorageClient Storage);
}

/// <summary>
/// Pre-computed crypto artifacts that must be inserted alongside a full-encrypted workspace
/// row. All three fields are mandatory and always co-present — either the caller has them
/// (Full storage) or they have none (None/Managed). Passed as a single nullable value
/// through the workspace creation pipeline so the pieces can never drift out of sync.
///
/// <see cref="StorageDekVersion"/> records which parent Storage DEK version the Workspace
/// DEK was derived from — persisted on the <c>wek_workspace_encryption_keys</c> row so the
/// read-side can line the wrap up with files written under that version.
/// </summary>
public sealed record WorkspaceFullEncryptionArtifacts(
    int StorageDekVersion,
    byte[] EncryptionSalt,
    byte[] OwnerWrappedWorkspaceDek);
