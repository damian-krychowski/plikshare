using PlikShare.Agents.Cache;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Mcp.Storages.List.Contracts;

namespace PlikShare.Mcp.Storages.List;

public class GetAgentStoragesQuery(PlikShareDb plikShareDb)
{
    public List<StorageItemDto> Execute(AgentContext agent)
    {
        using var connection = plikShareDb.OpenConnection();

        var storages = connection
            .Cmd(
                sql: """
                     SELECT
                         s_id,
                         s_external_id,
                         s_name,
                         (CASE WHEN s_encryption_type IS NULL THEN 'none' ELSE s_encryption_type END)
                     FROM s_storages
                     WHERE COALESCE(s_encryption_type, 'none') != 'full'
                     ORDER BY s_id ASC
                     """,
                readRowFunc: reader => new StorageRow(
                    Id: reader.GetInt32(0),
                    ExternalId: reader.GetString(1),
                    Name: reader.GetString(2),
                    EncryptionType: reader.GetString(3)))
            .Execute();

        return storages
            .Where(storage => agent.CanAccessStorage(storage.Id))
            .Select(storage => new StorageItemDto
            {
                StorageExternalId = storage.ExternalId,
                Name = storage.Name,
                EncryptionType = storage.EncryptionType
            })
            .ToList();
    }

    private readonly record struct StorageRow(
        int Id,
        string ExternalId,
        string Name,
        string EncryptionType);
}
