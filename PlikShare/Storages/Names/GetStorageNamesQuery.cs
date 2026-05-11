using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.Id;

namespace PlikShare.Storages.Names;

/// <summary>
/// Lightweight listing of all storages in the system, returning only the fields needed by
/// non-storage admins building storage-access UIs (general settings + user details). Does
/// NOT decrypt any storage details; the full <see cref="List.GetStoragesQuery"/> is admin-only
/// and remains the source for management screens.
/// </summary>
public class GetStorageNamesQuery(PlikShareDb plikShareDb)
{
    public List<GetStorageNamesItemDto> Execute()
    {
        using var connection = plikShareDb.OpenConnection();

        return connection
            .Cmd(
                sql: """
                     SELECT
                         s_external_id,
                         s_name,
                         s_encryption_type
                     FROM s_storages
                     ORDER BY s_id ASC
                     """,
                readRowFunc: reader => new GetStorageNamesItemDto
                {
                    ExternalId = reader.GetExtId<StorageExtId>(0),
                    Name = reader.GetString(1),
                    EncryptionType = StorageEncryptionExtensions
                        .FromDbValue(reader.GetStringOrNull(2))
                        .ToDbValue()
                })
            .Execute();
    }
}

public class GetStorageNamesResponseDto
{
    public required List<GetStorageNamesItemDto> Items { get; init; }
}

public class GetStorageNamesItemDto
{
    public required StorageExtId ExternalId { get; init; }
    public required string Name { get; init; }
    public required string EncryptionType { get; init; }
}
