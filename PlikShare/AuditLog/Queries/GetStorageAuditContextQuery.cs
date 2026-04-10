using PlikShare.AuditLog.Details;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Storages.Id;

namespace PlikShare.AuditLog.Queries;

public class GetStorageAuditContextQuery(PlikShareDb plikShareDb)
{
    public Audit.StorageRef? Execute(
        StorageExtId storageExternalId)
    {
        using var connection = plikShareDb.OpenConnection();

        var result = connection
            .OneRowCmd(
                sql: """
                    SELECT
                        s.s_name,
                        s.s_type
                    FROM s_storages AS s
                    WHERE s.s_external_id = $storageExternalId
                    LIMIT 1
                    """,
                readRowFunc: reader => new Audit.StorageRef
                {
                    ExternalId = storageExternalId,
                    Name = reader.GetString(0),
                    Type = reader.GetString(1)
                })
            .WithParameter("$storageExternalId", storageExternalId.Value)
            .Execute();

        return result.IsEmpty
            ? null
            : result.Value;
    }
}
