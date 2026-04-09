using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Files.Id;

namespace PlikShare.AuditLog.Queries;

public class GetFileAuditContextQuery(PlikShareDb plikShareDb)
{
    public AuditLogDetails.FileRef? Execute(
        FileExtId fileExternalId)
    {
        using var connection = plikShareDb.OpenConnection();

        var result = connection
            .OneRowCmd(
                sql: """
                    SELECT
                        fi.fi_name || fi.fi_extension,
                        fi.fi_size_in_bytes,
                        (
                            SELECT GROUP_CONCAT(af.fo_name, '/')
                            FROM (
                                SELECT fo_name
                                FROM fo_folders
                                WHERE (fo_id IN (SELECT value FROM json_each(f.fo_ancestor_folder_ids))
                                       OR fo_id = fi.fi_folder_id)
                                  AND fo_is_being_deleted = FALSE
                                ORDER BY json_array_length(fo_ancestor_folder_ids)
                            ) AS af
                        )
                    FROM fi_files AS fi
                    LEFT JOIN fo_folders AS f ON fi.fi_folder_id = f.fo_id
                    WHERE fi.fi_external_id = $fileExternalId
                    LIMIT 1
                    """,
                readRowFunc: reader => new AuditLogDetails.FileRef
                {
                    ExternalId = fileExternalId,
                    Name = reader.GetString(0),
                    SizeInBytes = reader.GetInt64(1),
                    FolderPath = reader.GetStringOrNull(2)
                })
            .WithParameter("$fileExternalId", fileExternalId.Value)
            .Execute();

        return result.IsEmpty
            ? null
            : result.Value;
    }

    public Dictionary<FileExtId, AuditLogDetails.FileRef> ExecuteMany(
        List<FileExtId> fileExternalIds)
    {
        if (fileExternalIds.Count == 0)
            return new Dictionary<FileExtId, AuditLogDetails.FileRef>();

        using var connection = plikShareDb.OpenConnection();

        var items = connection
            .Cmd(
                sql: """
                    SELECT
                        fi.fi_external_id,
                        fi.fi_name || fi.fi_extension,
                        fi.fi_size_in_bytes,
                        (
                            SELECT GROUP_CONCAT(af.fo_name, '/')
                            FROM (
                                SELECT fo_name
                                FROM fo_folders
                                WHERE (fo_id IN (SELECT value FROM json_each(f.fo_ancestor_folder_ids))
                                       OR fo_id = fi.fi_folder_id)
                                  AND fo_is_being_deleted = FALSE
                                ORDER BY json_array_length(fo_ancestor_folder_ids)
                            ) AS af
                        )
                    FROM fi_files AS fi
                    LEFT JOIN fo_folders AS f ON fi.fi_folder_id = f.fo_id
                    WHERE fi.fi_external_id IN (SELECT value FROM json_each($fileExternalIds))
                    """,
                readRowFunc: reader =>
                {
                    var externalId = new FileExtId(reader.GetString(0));

                    return new
                    {
                        ExternalId = externalId,
                        FileRef = new AuditLogDetails.FileRef
                        {
                            ExternalId = externalId,
                            Name = reader.GetString(1),
                            SizeInBytes = reader.GetInt64(2),
                            FolderPath = reader.GetStringOrNull(3)
                        }
                    };
                })
            .WithJsonParameter("$fileExternalIds", fileExternalIds)
            .Execute();

        return items.ToDictionary(x => x.ExternalId, x => x.FileRef);
    }
}
