using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using PlikShare.Files.Id;
using PlikShare.Workspaces.Cache;

namespace PlikShare.MediaProcessing.Generation;

/// <summary>
/// Resolves which of the requested files actually exist in the workspace AND are thumbnailable —
/// in a SINGLE query on ONE connection, instead of a per-file lookup. Only <c>fi_extension</c> is
/// decrypted app-side (the ciphertext can't be filtered in SQL); name/ancestors are never touched.
/// </summary>
public class GetThumbnailableFilesQuery(PlikShareDb plikShareDb)
{
    public List<FileExtId> Execute(
        WorkspaceContext workspace,
        List<string> fileExternalIds,
        WorkspaceEncryptionSession? workspaceEncryptionSession)
    {
        if (fileExternalIds.Count == 0)
            return [];

        using var connection = plikShareDb.OpenConnection();

        return connection
            .AggregateRows(
                sql: """
                    SELECT fi_external_id, fi_extension
                    FROM fi_files
                    WHERE fi_workspace_id = $workspaceId
                        AND fi_deleted_at IS NULL
                        AND fi_external_id IN (
                            SELECT value FROM json_each($fileExternalIds)
                        )
                    """,
                seed: new List<FileExtId>(fileExternalIds.Count),
                aggregateRowFunc: (acc, reader) =>
                {
                    var extension = reader.DecodeEncryptableString(1, workspaceEncryptionSession);

                    if (ContentTypeHelper.IsThumbnailable(extension))
                        acc.Add(reader.GetExtId<FileExtId>(0));

                    return acc;
                })
            .WithParameter("$workspaceId", workspace.Id)
            .WithJsonParameter("$fileExternalIds", fileExternalIds)
            .Execute();
    }
}
