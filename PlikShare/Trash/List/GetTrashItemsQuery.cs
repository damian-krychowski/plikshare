using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using PlikShare.Files.Id;
using PlikShare.Trash.List.Contracts;
using PlikShare.Workspaces.Cache;

namespace PlikShare.Trash.List;

public class GetTrashItemsQuery(
    PlikShareDb plikShareDb)
{
    public GetTrashItemsResponseDto Execute(
        WorkspaceContext workspace,
        WorkspaceEncryptionSession? workspaceEncryptionSession)
    {
        using var connection = plikShareDb.OpenConnection();

        // Only top-level (non-child) files surface in trash UI. Children (parent_file_id IS NOT NULL)
        // — thumbnails, OCR artifacts — are restored/purged together with their parent, never
        // surfaced as standalone trash entries.
        var rows = connection
            .Cmd(
                sql: """
                     SELECT
                        fi_external_id,
                        fi_name,
                        fi_extension,
                        fi_size_in_bytes,
                        fi_deleted_at,
                        fi_original_folder_path
                     FROM fi_files
                     WHERE fi_workspace_id = $workspaceId
                       AND fi_deleted_at IS NOT NULL
                       AND fi_parent_file_id IS NULL
                     ORDER BY fi_deleted_at DESC, fi_id DESC
                     """,
                readRowFunc: reader => new RawRow(
                    ExternalId: reader.GetExtId<FileExtId>(0),
                    Name: reader.DecodeEncryptableString(1, workspaceEncryptionSession),
                    Extension: reader.DecodeEncryptableString(2, workspaceEncryptionSession),
                    SizeInBytes: reader.GetInt64(3),
                    DeletedAt: reader.GetDateTimeOffset(4),
                    Path: reader.GetFromJsonOrNull<List<OriginalFolderPathSegment>>(5)
                        ?.Select(s => s with
                        {
                            Name = workspaceEncryptionSession.DecodeMetadata(s.Name)
                        })
                        .ToList()))
            .WithParameter("$workspaceId", workspace.Id)
            .Execute();

        var items = new List<TrashItemDto>(
            rows.Count);

        long totalSize = 0;

        foreach (var row in rows)
        {
            totalSize += row.SizeInBytes;

            var pathSegments = ParsePath(row.Path);

            items.Add(new TrashItemDto
            {
                ExternalId = row.ExternalId,
                Name = row.Name,
                Extension = row.Extension,
                SizeInBytes = row.SizeInBytes,
                DeletedAt = row.DeletedAt,
                OriginalFolderPath = pathSegments,

                AutoDeletesAt = workspace
                    .TrashPolicy
                    .AutoDeleteMoment(row.DeletedAt),
            });
        }

        return new GetTrashItemsResponseDto
        {
            Items = items,
            TotalSizeInBytes = totalSize
        };
    }

    private static List<string>? ParsePath(
        List<OriginalFolderPathSegment>? path)
    {
        if (path is null || path.Count == 0)
            return null;

        return path
            .Select(segment => segment.Name)
            .ToList();
    }

    private readonly record struct RawRow(
        FileExtId ExternalId,
        string Name,
        string Extension,
        long SizeInBytes,
        DateTimeOffset DeletedAt,
        List<OriginalFolderPathSegment>? Path);
}
