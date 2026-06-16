using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Files.Id;

namespace PlikShare.Mcp.Files.Get;

public class GetFileForAgentQuery(PlikShareDb plikShareDb)
{
    public sealed record Result(
        string WorkspaceExternalId,
        string ExternalId,
        string Name,
        string Extension,
        string ContentType,
        long SizeInBytes,
        DateTime? CreatedAt,
        List<FilePathItem> Path);

    public sealed record FilePathItem(
        string ExternalId,
        string Name);

    public Result? Execute(FileExtId fileExternalId)
    {
        using var connection = plikShareDb.OpenConnection();

        var result = connection
            .OneRowCmd(
                sql: """
                     SELECT
                         w.w_external_id,
                         fi.fi_external_id,
                         fi.fi_name,
                         fi.fi_extension,
                         fi.fi_content_type,
                         fi.fi_size_in_bytes,
                         fi.fi_created_at,
                         (
                             SELECT json_group_array(json_object(
                                 'externalId', sub.fo_external_id,
                                 'name', sub.fo_name
                             ))
                             FROM (
                                 SELECT af.fo_external_id, af.fo_name
                                 FROM fo_folders AS af
                                 WHERE (
                                         af.fo_id IN (SELECT value FROM json_each(f.fo_ancestor_folder_ids))
                                         OR af.fo_id = fi.fi_folder_id
                                     )
                                     AND af.fo_workspace_id = fi.fi_workspace_id
                                     AND af.fo_is_being_deleted = FALSE
                                 ORDER BY json_array_length(af.fo_ancestor_folder_ids)
                             ) AS sub
                         ) AS path_json
                     FROM fi_files AS fi
                     INNER JOIN w_workspaces AS w ON w.w_id = fi.fi_workspace_id
                     LEFT JOIN fo_folders AS f ON f.fo_id = fi.fi_folder_id
                     WHERE
                         fi.fi_external_id = $fileExternalId
                         AND fi.fi_parent_file_id IS NULL
                         AND fi.fi_deleted_at IS NULL
                         AND fi.fi_is_upload_completed = TRUE
                     """,
                readRowFunc: reader => new Result(
                    WorkspaceExternalId: reader.GetString(0),
                    ExternalId: reader.GetString(1),
                    Name: reader.DecodeEncryptableString(2, null),
                    Extension: reader.DecodeEncryptableString(3, null),
                    ContentType: reader.DecodeEncryptableString(4, null),
                    SizeInBytes: reader.GetInt64(5),
                    CreatedAt: reader.GetDateTimeOffsetOrNull(6)?.UtcDateTime,
                    Path: reader.GetFromJsonOrNull<List<FilePathItem>>(7) ?? []),
                name: "agent.get_file")
            .WithParameter("$fileExternalId", fileExternalId.Value)
            .Execute();

        return result.IsEmpty ? null : result.Value;
    }
}
