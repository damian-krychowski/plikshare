using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;

namespace PlikShare.Workspaces.Encryption;

/// <summary>
/// Reads the per-user wrap of a Workspace DEK from <c>wek_workspace_encryption_keys</c>.
/// Returns the sealed-box ciphertext the caller can unwrap with their X25519 private key,
/// or null when the user has no access to that workspace's encrypted data.
/// </summary>
public class WorkspaceEncryptionKeyReader(PlikShareDb plikShareDb)
{
    public byte[]? TryLoadWrappedDek(int workspaceId, int userId)
    {
        using var connection = plikShareDb.OpenConnection();

        var (isEmpty, wrappedDek) = connection
            .OneRowCmd(
                sql: """
                     SELECT wek_wrapped_workspace_dek
                     FROM wek_workspace_encryption_keys
                     WHERE wek_workspace_id = $workspaceId
                       AND wek_user_id = $userId
                     LIMIT 1
                     """,
                readRowFunc: reader => reader.GetFieldValue<byte[]>(0))
            .WithParameter("$workspaceId", workspaceId)
            .WithParameter("$userId", userId)
            .Execute();

        return isEmpty ? null : wrappedDek;
    }

    public List<int> ListWorkspaceMemberIds(int workspaceId)
    {
        using var connection = plikShareDb.OpenConnection();

        return connection
            .Cmd(
                sql: """
                     SELECT wek_user_id
                     FROM wek_workspace_encryption_keys
                     WHERE wek_workspace_id = $workspaceId
                     ORDER BY wek_user_id
                     """,
                readRowFunc: reader => reader.GetInt32(0))
            .WithParameter("$workspaceId", workspaceId)
            .Execute();
    }
}
