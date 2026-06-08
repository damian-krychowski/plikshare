using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using PlikShare.Files.Id;
using PlikShare.Workspaces.Cache;

namespace PlikShare.MediaProcessing.Dimensions;

public class UpsertParentImageDimensionsQuery(DbWriteQueue dbWriteQueue)
{
    public readonly record struct DimensionsUpdate(
        FileExtId FileExternalId,
        EncodedMetadataValue EncodedMetadata);

    public Task ExecuteBatch(
        WorkspaceContext workspace,
        IReadOnlyList<DimensionsUpdate> items,
        CancellationToken cancellationToken)
    {
        if (items.Count == 0)
            return Task.CompletedTask;

        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteBatchOperation(
                dbWriteContext: context,
                workspace: workspace,
                items: items),
            cancellationToken: cancellationToken);
    }

    private static void ExecuteBatchOperation(
        SqliteWriteContext dbWriteContext,
        WorkspaceContext workspace,
        IReadOnlyList<DimensionsUpdate> items)
    {
        var entities = items
            .Select(item => new DimensionsUpdateEntity(
                FileExternalId: item.FileExternalId.Value,
                Metadata: item.EncodedMetadata.Encoded))
            .ToList();

        dbWriteContext
            .Cmd(
                sql: """
                     UPDATE fi_files
                     SET fi_metadata = (
                         SELECT CAST(json_extract(item.value, '$.metadata') AS BLOB)
                         FROM json_each($items) AS item
                         WHERE json_extract(item.value, '$.fileExternalId') = fi_files.fi_external_id
                     )
                     WHERE fi_files.fi_workspace_id = $workspaceId
                       AND fi_files.fi_external_id IN (
                           SELECT json_extract(value, '$.fileExternalId')
                           FROM json_each($items)
                       )
                       AND fi_files.fi_parent_file_id IS NULL
                       AND fi_files.fi_deleted_at IS NULL
                     RETURNING fi_id
                     """,
                readRowFunc: reader => reader.GetInt32(0))
            .WithJsonParameter("$items", entities)
            .WithParameter("$workspaceId", workspace.Id)
            .Execute();
    }

    private sealed record DimensionsUpdateEntity(
        string FileExternalId,
        string Metadata);
}
