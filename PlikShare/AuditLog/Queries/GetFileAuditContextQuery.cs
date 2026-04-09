using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Files.Id;

namespace PlikShare.AuditLog.Queries;

public class GetFileAuditContextQuery(PlikShareDb plikShareDb)
{
    public FileAuditContext? Execute(
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
                readRowFunc: reader => new FileAuditContext
                {
                    FileName = reader.GetString(0),
                    SizeInBytes = reader.GetInt64(1),
                    FolderPath = reader.GetStringOrNull(2)
                })
            .WithParameter("$fileExternalId", fileExternalId.Value)
            .Execute();

        return result.IsEmpty
            ? null
            : result.Value;
    }

    public Dictionary<FileExtId, FileAuditContext> ExecuteMany(
        List<FileExtId> fileExternalIds)
    {
        if (fileExternalIds.Count == 0)
            return new Dictionary<FileExtId, FileAuditContext>();

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
                readRowFunc: reader => new
                {
                    ExternalId = new FileExtId(reader.GetString(0)),
                    Context = new FileAuditContext
                    {
                        FileName = reader.GetString(1),
                        SizeInBytes = reader.GetInt64(2),
                        FolderPath = reader.GetStringOrNull(3)
                    }
                })
            .WithJsonParameter("$fileExternalIds", fileExternalIds)
            .Execute();

        return items.ToDictionary(x => x.ExternalId, x => x.Context);
    }
}

public class FileAuditContext
{
    public required string FileName { get; init; }
    public required long SizeInBytes { get; init; }
    public string? FolderPath { get; init; }

    public AuditLogDetails.FileRef ToFileRef(FileExtId externalId) => new()
    {
        ExternalId = externalId,
        Name = FileName,
        SizeInBytes = SizeInBytes,
        FolderPath = FolderPath
    };
}
