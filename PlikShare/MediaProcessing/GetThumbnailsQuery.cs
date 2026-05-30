using Microsoft.Data.Sqlite;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using PlikShare.Files.Id;
using PlikShare.Files.Metadata;
using PlikShare.Workspaces.Cache;

namespace PlikShare.MediaProcessing;

public class GetThumbnailsQuery(PlikShareDb plikShareDb)
{
    public List<Thumbnail> Execute(
        WorkspaceContext workspace,
        FileExtId parentFileExternalId,
        WorkspaceEncryptionSession? workspaceEncryptionSession)
    {
        using var connection = plikShareDb.OpenConnection();

        return Execute(
            workspace,
            parentFileExternalId,
            workspaceEncryptionSession,
            connection);
    }

    public List<Thumbnail> Execute(
        WorkspaceContext workspace,
        FileExtId parentFileExternalId,
        WorkspaceEncryptionSession? workspaceEncryptionSession,
        SqliteConnection connection)
    {
        // is_upload_completed filter skips in-flight uploads — incomplete rows are normal
        // mid-upload state and shouldn't be visible to readers or targeted for replacement.
        // ORDER BY fi_id DESC gives newest-first so callers that dedup by variant pick the
        // latest write (user's "replace by upload" semantics).
        return connection
            .AggregateRows(
                sql: @"
                    SELECT
                        child_fi.fi_id,
                        child_fi.fi_external_id,
                        child_fi.fi_size_in_bytes,
                        child_fi.fi_metadata
                    FROM fi_files AS child_fi
                    INNER JOIN fi_files AS parent_fi
                        ON parent_fi.fi_id = child_fi.fi_parent_file_id
                    WHERE
                        parent_fi.fi_external_id = $parentExternalId
                        AND parent_fi.fi_workspace_id = $workspaceId
                        AND parent_fi.fi_deleted_at IS NULL
                        AND child_fi.fi_workspace_id = $workspaceId
                        AND child_fi.fi_deleted_at IS NULL
                        AND child_fi.fi_is_upload_completed = TRUE
                        AND child_fi.fi_metadata IS NOT NULL
                    ORDER BY child_fi.fi_id DESC
                ",
                seed: new List<Thumbnail>(),
                aggregateRowFunc: (acc, reader) =>
                {
                    var metadataJson = reader.DecodeEncryptableBlobOrNull(
                        3,
                        workspaceEncryptionSession);

                    if (metadataJson is null)
                        return acc;

                    var metadata = Json.Deserialize<FileMetadata>(
                        metadataJson);

                    if (metadata is not ThumbnailFileMetadata thumbnailMetadata)
                        return acc;

                    acc.Add(new Thumbnail
                    {
                        Id = reader.GetInt32(0),
                        ExternalId = reader.GetExtId<FileExtId>(1),
                        SizeInBytes = reader.GetInt64(2),
                        Variant = thumbnailMetadata.Variant
                    });

                    return acc;
                })
            .WithParameter("$parentExternalId", parentFileExternalId.Value)
            .WithParameter("$workspaceId", workspace.Id)
            .Execute();
    }

    public class Thumbnail
    {
        public required int Id { get; init; }
        public required FileExtId ExternalId { get; init; }
        public required long SizeInBytes { get; init; }
        public required ThumbnailVariant Variant { get; init; }
    }
}
