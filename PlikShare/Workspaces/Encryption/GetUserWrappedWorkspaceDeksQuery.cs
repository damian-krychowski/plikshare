using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Workspaces.Id;

namespace PlikShare.Workspaces.Encryption;

/// <summary>
/// Reads per-user per-version wraps of a Workspace DEK from
/// <c>wek_workspace_encryption_keys</c>. After a Storage DEK rotation every existing
/// workspace member gains a new wrap row for the new Storage DEK version, so a user with
/// access holds one sealed-box wrap per version they can still read.
/// </summary>
public class GetUserWrappedWorkspaceDeksQuery(PlikShareDb plikShareDb)
{
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

    public List<WrappedDekForWorkspaceRow> GetAllWrappedDeksForUser(int userId)
    {
        using var connection = plikShareDb.OpenConnection();

        return connection
            .Cmd(
                sql: """
                     SELECT
                         wek.wek_workspace_id,
                         w.w_external_id,
                         wek.wek_storage_dek_version,
                         wek.wek_wrapped_workspace_dek
                     FROM wek_workspace_encryption_keys wek
                     INNER JOIN w_workspaces w ON w.w_id = wek.wek_workspace_id
                     WHERE wek.wek_user_id = $userId
                       AND w.w_is_being_deleted = FALSE
                     ORDER BY wek.wek_workspace_id, wek.wek_storage_dek_version
                     """,
                readRowFunc: reader => new WrappedDekForWorkspaceRow(
                    WorkspaceId: reader.GetInt32(0),
                    WorkspaceExternalId: new WorkspaceExtId(reader.GetString(1)),
                    StorageDekVersion: reader.GetInt32(2),
                    WrappedDek: reader.GetFieldValue<byte[]>(3)))
            .WithParameter("$userId", userId)
            .Execute();
    }

    public readonly record struct WrappedDekRow(
        int StorageDekVersion,
        byte[] WrappedDek);

    public readonly record struct WrappedDekForWorkspaceRow(
        int WorkspaceId,
        WorkspaceExtId WorkspaceExternalId,
        int StorageDekVersion,
        byte[] WrappedDek);
}
