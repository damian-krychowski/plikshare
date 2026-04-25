using PlikShare.AuditLog.Details;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using PlikShare.Files.Id;
using PlikShare.Uploads.Id;

namespace PlikShare.AuditLog.Queries;

public class GetFileUploadAuditContextQuery(PlikShareDb plikShareDb)
{
    public Dictionary<FileUploadExtId, Audit.FileUploadRef> ExecuteMany(
        List<FileUploadExtId> fileUploadExternalIds)
    {
        if (fileUploadExternalIds.Count == 0)
            return new Dictionary<FileUploadExtId, Audit.FileUploadRef>();

        using var connection = plikShareDb.OpenConnection();

        var items = connection
            .Cmd(
                sql: """
                    SELECT
                        fu.fu_external_id,
                        fu.fu_file_external_id,
                        fu.fu_file_name,
                        fu.fu_file_extension,
                        fu.fu_file_size_in_bytes,
                        (
                            SELECT json_group_array(af.fo_name)
                            FROM (
                                SELECT fo_name
                                FROM fo_folders
                                WHERE (fo_id IN (SELECT value FROM json_each(f.fo_ancestor_folder_ids))
                                       OR fo_id = fu.fu_folder_id)
                                ORDER BY json_array_length(fo_ancestor_folder_ids)
                            ) AS af
                        )
                    FROM fu_file_uploads AS fu
                    LEFT JOIN fo_folders AS f ON fu.fu_folder_id = f.fo_id
                    WHERE fu.fu_external_id IN (SELECT value FROM json_each($fileUploadExternalIds))
                    """,
                readRowFunc: reader =>
                {
                    var externalId = new FileUploadExtId(reader.GetString(0));
                    var ancestors = reader.GetFromJsonOrNull<List<EncodedMetadataValue>>(5);

                    return new
                    {
                        ExternalId = externalId,
                        FileUploadRef = new Audit.FileUploadRef
                        {
                            ExternalId = externalId,
                            FileExternalId = new FileExtId(reader.GetString(1)),
                            Name = reader.GetEncodedMetadata(2),
                            Extension = reader.GetEncodedMetadata(3),
                            SizeInBytes = reader.GetInt64(4),
                            FolderPath = ancestors is null or { Count: 0 }
                                ? null
                                : ancestors
                        }
                    };
                })
            .WithJsonParameter("$fileUploadExternalIds", fileUploadExternalIds)
            .Execute();

        return items.ToDictionary(x => x.ExternalId, x => x.FileUploadRef);
    }
}
