using PlikShare.Storages.Encryption;
using PlikShare.Users.Cache;
using PlikShare.Workspaces.Cache;

namespace PlikShare.Core.Encryption;

public class WorkspaceDekUnsealException(
    int workspaceId,
    int storageDekVersion,
    Exception innerException) : Exception(
    $"Failed to unseal workspace DEK (WorkspaceId={workspaceId}, StorageDekVersion={storageDekVersion}).",
    innerException)
{
    public int WorkspaceId { get; } = workspaceId;
    public int StorageDekVersion { get; } = storageDekVersion;
}

public class StorageDekUnsealException(
    int storageId,
    int storageDekVersion,
    Exception innerException) : Exception(
    $"Failed to unseal storage DEK (StorageId={storageId}, StorageDekVersion={storageDekVersion}).",
    innerException)
{
    public int StorageId { get; } = storageId;
    public int StorageDekVersion { get; } = storageDekVersion;
}

public static class UserContextEncryptionExtensions
{
    extension(UserWrappedStorageDek wrap)
    {
        public StorageDekEntry Unseal(SecureBytes privateKey)
        {
            try
            {
                return new StorageDekEntry
                {
                    Dek = UserKeyPair.OpenSealed(
                        recipientPrivateKey: privateKey,
                        @sealed: wrap.WrappedStorageDek),

                    DekVersion = wrap.StorageDekVersion
                };
            }
            catch (Exception ex) when (ex is not StorageDekUnsealException)
            {
                throw new StorageDekUnsealException(
                    storageId: wrap.StorageId,
                    storageDekVersion: wrap.StorageDekVersion,
                    innerException: ex);
            }
        }
    }

    extension(UserWrappedWorkspaceDek wrap)
    {
        public WorkspaceDekEntry Unseal(SecureBytes privateKey)
        {
            try
            {
                return new WorkspaceDekEntry
                {
                    Dek = UserKeyPair.OpenSealed(
                        recipientPrivateKey: privateKey,
                        @sealed: wrap.WrappedWorkspaceDek),

                    StorageDekVersion = wrap.StorageDekVersion
                };
            }
            catch (Exception ex) when (ex is not WorkspaceDekUnsealException)
            {
                throw new WorkspaceDekUnsealException(
                    workspaceId: wrap.WorkspaceId,
                    storageDekVersion: wrap.StorageDekVersion,
                    innerException: ex);
            }
        }
    }

    extension(IList<UserWrappedWorkspaceDek> wraps)
    {
        public WorkspaceDekEntry[] Unseal(SecureBytes privateKey)
        {
            var entries = new WorkspaceDekEntry[wraps.Count];

            for (var index = 0; index < wraps.Count; index++)
            {
                var wrap = wraps[index];

                try
                {
                    entries[index] = wrap.Unseal(privateKey);
                }
                catch
                {
                    for (var i = 0; i < index; i++)
                        entries[i].Dispose();

                    throw;
                }
            }

            return entries;
        }
    }

    extension(UserContext userContext)
    {
        public UserWrappedStorageDek? TryGetLatestStorageDek(
            int storageId)
        {
            return userContext
                .WrappedStorageDeks
                .Where(dek => dek.StorageId == storageId)
                .OrderByDescending(dek => dek.StorageDekVersion)
                .FirstOrDefault();
        }

        public WorkspaceDekEntry[] UnsealWorkspaceDeks(
            WorkspaceContext workspace,
            SecureBytes privateKey)
        {
            if (workspace.Storage.Encryption is not FullStorageEncryption)
                throw new InvalidOperationException(
                    $"Cannot get WorkspaceDek for Workspace#{workspace.Id} because it does not use full-encryption");

            if (workspace.EncryptionMetadata is null)
                throw new InvalidOperationException(
                    $"Workspace#{workspace.Id} does not have EncryptionMetadata even though it's using full-encryption");

            var wrappedWorkspaceDeks = userContext
                .WrappedWorkspaceDeks
                .Where(wrap => wrap.WorkspaceId == workspace.Id)
                .ToArray();

            var wrappedStorageDeks = userContext
                .WrappedStorageDeks
                .Where(wrap => wrap.StorageId == workspace.Storage.StorageId)
                .ToArray();

            if (wrappedWorkspaceDeks.Length == 0 && wrappedStorageDeks.Length == 0)
                return [];

            var fromWorkspaceWraps = wrappedWorkspaceDeks.Unseal(privateKey);

            WorkspaceDekEntry[] fromStorageWraps;
            try
            {
                var derivable = wrappedStorageDeks
                    .Where(sDek => wrappedWorkspaceDeks.All(wDek => sDek.StorageDekVersion != wDek.StorageDekVersion))
                    .ToArray();

                fromStorageWraps = new WorkspaceDekEntry[derivable.Length];

                for (var i = 0; i < derivable.Length; i++)
                {
                    try
                    {
                        using var storageDek = derivable[i].Unseal(privateKey);

                        fromStorageWraps[i] = storageDek.DeriveWorkspaceDek(
                            [workspace.EncryptionMetadata.Salt]);
                    }
                    catch
                    {
                        for (var j = 0; j < i; j++)
                            fromStorageWraps[j].Dispose();

                        throw;
                    }
                }
            }
            catch
            {
                foreach (var entry in fromWorkspaceWraps)
                    entry.Dispose();

                throw;
            }

            return
            [
                .. fromWorkspaceWraps,
                .. fromStorageWraps
            ];
        }
    }
}