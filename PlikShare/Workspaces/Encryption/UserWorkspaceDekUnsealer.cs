using System.Security.Cryptography;
using PlikShare.Core.Encryption;
using Serilog;

namespace PlikShare.Workspaces.Encryption;

/// <summary>
/// Loads every <c>wek_workspace_encryption_keys</c> wrap that belongs to a given user on a
/// given workspace and unseals each one with the caller-supplied X25519 private key.
///
/// Pure read + crypto — no DB writes, no HTTP concerns, no HttpContext. The caller owns
/// the private-key buffer (it is not zeroed here); the returned <see cref="WorkspaceDekEntry"/>
/// array is fresh and its plaintext DEKs are zeroed on partial-failure rollback before the
/// method returns.
/// </summary>
public class UserWorkspaceDekUnsealer(
    GetUserWrappedWorkspaceDeksQuery getUserWrappedWorkspaceDeksQuery)
{
    public Result UnsealForUser(
        int workspaceId,
        int userId,
        SecureBytes privateKey)
    {
        var wrappedDeks = getUserWrappedWorkspaceDeksQuery.GetWrappedDeksForUser(
            workspaceId: workspaceId,
            userId: userId);

        if (wrappedDeks.Count == 0)
            return new Result(Code: ResultCode.NoWraps);

        var entries = new List<WorkspaceDekEntry>(
            capacity: wrappedDeks.Count);

        foreach (var row in wrappedDeks)
        {
            SecureBytes dek;

            try
            {
                dek = UserKeyPair.OpenSealed(
                    recipientPrivateKey: privateKey,
                    @sealed: row.WrappedDek);
            }
            catch (Exception e)
            {
                // A corrupted or tamper-induced wrap will fail the sealed-box AEAD check.
                // Treat the whole unseal as failed and wipe anything we already produced.
                Log.Error(e,
                    "Unsealing wrapped Workspace DEK v{Version} failed for User#{UserId} on Workspace#{WorkspaceId}.",
                    row.StorageDekVersion, userId, workspaceId);

                foreach (var entry in entries)
                    entry.Dek.Dispose();

                return new Result(Code: ResultCode.UnsealFailed);
            }

            entries.Add(new WorkspaceDekEntry
            {
                WorkspaceId = workspaceId,
                StorageDekVersion = row.StorageDekVersion,
                Salt = row.Salt,
                Dek = dek
            });
        }

        return new Result(
            Code: ResultCode.Ok,
            Entries: entries.ToArray());
    }

    public enum ResultCode
    {
        Ok = 0,
        NoWraps,
        UnsealFailed
    }

    public readonly record struct Result(
        ResultCode Code,
        WorkspaceDekEntry[]? Entries = null);
}
