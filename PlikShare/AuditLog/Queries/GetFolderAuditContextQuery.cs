using PlikShare.AuditLog.Details;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Folders.Id;

namespace PlikShare.AuditLog.Queries;

public class GetFolderAuditContextQuery(PlikShareDb plikShareDb)
{
    public Audit.FolderRef? Execute(
        FolderExtId folderExternalId)
    {
        using var connection = plikShareDb.OpenConnection();

        var result = connection
            .OneRowCmd(
                sql: """
                    SELECT
                        f.fo_name,
                        (
                            SELECT json_group_array(af.fo_name)
                            FROM (
                                SELECT fo_name
                                FROM fo_folders
                                WHERE fo_id IN (SELECT value FROM json_each(f.fo_ancestor_folder_ids))
                                ORDER BY json_array_length(fo_ancestor_folder_ids)
                            ) AS af
                        )
                    FROM fo_folders AS f
                    WHERE f.fo_external_id = $folderExternalId
                    LIMIT 1
                    """,
                readRowFunc: reader =>
                {
                    var ancestors = reader.GetFromJsonOrNull<List<string>>(1);

                    return new Audit.FolderRef
                    {
                        ExternalId = folderExternalId,
                        Name = reader.GetString(0),
                        FolderPath = ancestors is null or { Count: 0 }
                            ? null
                            : ancestors
                    };
                })
            .WithParameter("$folderExternalId", folderExternalId.Value)
            .Execute();

        return result.IsEmpty
            ? null
            : result.Value;
    }

    public Dictionary<FolderExtId, Audit.FolderRef> ExecuteMany(
        List<FolderExtId> folderExternalIds)
    {
        if (folderExternalIds.Count == 0)
            return new Dictionary<FolderExtId, Audit.FolderRef>();

        using var connection = plikShareDb.OpenConnection();

        var items = connection
            .Cmd(
                sql: """
                    SELECT
                        f.fo_external_id,
                        f.fo_name,
                        (
                            SELECT json_group_array(af.fo_name)
                            FROM (
                                SELECT fo_name
                                FROM fo_folders
                                WHERE fo_id IN (SELECT value FROM json_each(f.fo_ancestor_folder_ids))
                                ORDER BY json_array_length(fo_ancestor_folder_ids)
                            ) AS af
                        )
                    FROM fo_folders AS f
                    WHERE f.fo_external_id IN (SELECT value FROM json_each($folderExternalIds))
                    """,
                readRowFunc: reader =>
                {
                    var externalId = new FolderExtId(reader.GetString(0));
                    var ancestors = reader.GetFromJsonOrNull<List<string>>(2);

                    return new
                    {
                        ExternalId = externalId,
                        FolderRef = new Audit.FolderRef
                        {
                            ExternalId = externalId,
                            Name = reader.GetString(1),
                            FolderPath = ancestors is null or { Count: 0 }
                                ? null
                                : ancestors
                        }
                    };
                })
            .WithJsonParameter("$folderExternalIds", folderExternalIds)
            .Execute();

        return items.ToDictionary(x => x.ExternalId, x => x.FolderRef);
    }
}
