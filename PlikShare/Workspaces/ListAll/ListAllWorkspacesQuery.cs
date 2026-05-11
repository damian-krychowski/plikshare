using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Storages.Id;
using PlikShare.Users.Id;
using PlikShare.Workspaces.Id;
using PlikShare.Workspaces.ListAll.Contracts;

namespace PlikShare.Workspaces.ListAll;

/// <summary>
/// Admin-only listing of every workspace in the system. Backs the workspace picker on the
/// user-details page where an admin assigns a target user to an existing workspace; the
/// optional <paramref name="excludeMemberOrOwnerUserId"/> hides workspaces the target is
/// already in (owner or member) so the picker only offers actionable candidates.
/// </summary>
public class ListAllWorkspacesQuery(PlikShareDb plikShareDb)
{
    public GetAllWorkspacesResponseDto Execute(
        int? excludeMemberOrOwnerUserId)
    {
        using var connection = plikShareDb.OpenConnection();

        var items = connection
            .Cmd(
                sql: """
                     SELECT
                         w_external_id,
                         w_name,
                         w_current_size_in_bytes,
                         w_max_size_in_bytes,
                         w_is_bucket_created,
                         storage.s_external_id,
                         storage.s_name,
                         storage.s_encryption_type,
                         owner.u_external_id,
                         owner.u_email
                     FROM w_workspaces
                     INNER JOIN s_storages AS storage
                         ON storage.s_id = w_storage_id
                     INNER JOIN u_users AS owner
                         ON owner.u_id = w_owner_id
                     WHERE
                         w_is_being_deleted = FALSE
                         AND ($excludeUserId IS NULL OR w_owner_id != $excludeUserId)
                         AND ($excludeUserId IS NULL OR NOT EXISTS (
                             SELECT 1
                             FROM wm_workspace_membership
                             WHERE wm_workspace_id = w_id
                               AND wm_member_id = $excludeUserId
                         ))
                     ORDER BY
                         w_id ASC
                     """,
                readRowFunc: reader => new GetAllWorkspacesItemDto
                {
                    ExternalId = reader.GetExtId<WorkspaceExtId>(0),
                    Name = reader.GetString(1),
                    CurrentSizeInBytes = reader.GetInt64(2),
                    MaxSizeInBytes = reader.GetInt64OrNull(3),
                    IsBucketCreated = reader.GetBoolean(4),
                    Storage = new GetAllWorkspacesStorageDto
                    {
                        ExternalId = reader.GetExtId<StorageExtId>(5),
                        Name = reader.GetString(6),
                        EncryptionType = reader.GetString(7)
                    },
                    Owner = new GetAllWorkspacesOwnerDto
                    {
                        ExternalId = reader.GetExtId<UserExtId>(8),
                        Email = reader.GetString(9)
                    }
                })
            .WithParameter<int?>("$excludeUserId", excludeMemberOrOwnerUserId)
            .Execute();

        return new GetAllWorkspacesResponseDto
        {
            Items = items
        };
    }
}
