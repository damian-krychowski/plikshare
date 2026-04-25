using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;

namespace PlikShare.Workspaces.Encryption;

/// <summary>
/// Reads per-user per-version wraps of a Workspace DEK from
/// <c>wek_workspace_encryption_keys</c>. After a Storage DEK rotation every existing
/// workspace member gains a new wrap row for the new Storage DEK version, so a user with
/// access holds one sealed-box wrap per version they can still read.
/// </summary>
public class GetUserWrappedWorkspaceDeksQuery(PlikShareDb plikShareDb)
{
    /// <summary>
    /// Loads every wrap the user holds for this workspace, one row per Storage DEK version.
    /// The read-side filter unseals the whole set eagerly so subsequent file operations
    /// pick the right DEK by the version recorded in each file header.
    /// </summary>
    public List<WrappedDekRow> GetWrappedDeksForUser(int workspaceId, int userId)
    {
        using var connection = plikShareDb.OpenConnection();

        return connection
            .Cmd(
                sql: """
                     SELECT
                         wek.wek_storage_dek_version,
                         wek.wek_wrapped_workspace_dek
                     FROM wek_workspace_encryption_keys wek
                     WHERE wek.wek_workspace_id = $workspaceId
                       AND wek.wek_user_id = $userId
                     ORDER BY wek.wek_storage_dek_version
                     """,
                readRowFunc: reader => new WrappedDekRow(
                    StorageDekVersion: reader.GetInt32(0),
                    WrappedDek: reader.GetFieldValue<byte[]>(1)))
            .WithParameter("$workspaceId", workspaceId)
            .WithParameter("$userId", userId)
            .Execute();
    }

    public readonly record struct WrappedDekRow(
        int StorageDekVersion,
        byte[] WrappedDek);
}
