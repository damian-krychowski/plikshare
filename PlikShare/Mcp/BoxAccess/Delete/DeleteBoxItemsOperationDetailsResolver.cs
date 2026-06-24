using PlikShare.Agents.Operations;
using PlikShare.AuditLog;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using PlikShare.Mcp.BoxAccess.Delete.Contracts;

namespace PlikShare.Mcp.BoxAccess.Delete;

/// <summary>
/// Resolves a delete_box_items operation's stored ids into the box name and the actual folder and file
/// names (and their paths and parent folders) so a human can see exactly what the agent wants to delete.
/// </summary>
public class DeleteBoxItemsOperationDetailsResolver(
    BoxApprovalNameResolver boxNameResolver,
    AuditLogService auditLogService,
    PlikShareDb plikShareDb)
{
    public DeleteBoxItemsOperationDetails Resolve(AgentOperation operation)
    {
        var parameters = Json.Deserialize<DeleteBoxItemsParams>(operation.ParamsJson)
            ?? throw new InvalidOperationException("The stored operation parameters were invalid.");

        var items = auditLogService.GetBulkItemsContext(
            folderExternalIds: parameters.FolderExternalIds.ToList(),
            fileExternalIds: parameters.FileExternalIds.ToList(),
            fileUploadExternalIds: []);

        var parentFolderByFile = GetParentFolders(parameters.FileExternalIds);

        return new DeleteBoxItemsOperationDetails
        {
            BoxExternalId = parameters.BoxExternalId,
            BoxName = boxNameResolver.GetBoxName(parameters.BoxExternalId),

            Folders = items.Folders
                .Select(folder => new DeleteBoxItemsOperationDetails.FolderToDelete
                {
                    ExternalId = folder.ExternalId.Value,
                    Name = folder.Name.Encoded,
                    Path = BuildPath(folder.FolderPath)
                })
                .ToList(),

            Files = items.Files
                .Select(file => new DeleteBoxItemsOperationDetails.FileToDelete
                {
                    ExternalId = file.ExternalId.Value,
                    FolderExternalId = parentFolderByFile.GetValueOrDefault(file.ExternalId.Value),
                    Name = $"{file.Name.Encoded}{file.Extension.Encoded}",
                    Path = BuildPath(file.FolderPath)
                })
                .ToList()
        };
    }

    private Dictionary<string, string?> GetParentFolders(IReadOnlyList<string> fileExternalIds)
    {
        if (fileExternalIds.Count == 0)
            return [];

        using var connection = plikShareDb.OpenConnection();

        var rows = connection
            .Cmd(
                sql: """
                     SELECT fi.fi_external_id, parent.fo_external_id
                     FROM fi_files AS fi
                     LEFT JOIN fo_folders AS parent ON fi.fi_folder_id = parent.fo_id
                     WHERE fi.fi_external_id IN (SELECT value FROM json_each($fileExternalIds))
                     """,
                readRowFunc: reader => new
                {
                    FileExternalId = reader.GetString(0),
                    FolderExternalId = reader.GetStringOrNull(1)
                })
            .WithParameter("$fileExternalIds", Json.Serialize(fileExternalIds))
            .Execute();

        return rows.ToDictionary(
            row => row.FileExternalId,
            row => row.FolderExternalId);
    }

    private static string? BuildPath(List<EncodedMetadataValue>? path) =>
        path is null or { Count: 0 }
            ? null
            : string.Join(" / ", path.Select(segment => segment.Encoded));
}
