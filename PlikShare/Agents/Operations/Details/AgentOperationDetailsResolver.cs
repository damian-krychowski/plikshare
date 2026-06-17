using System.Text.Json;
using PlikShare.Agents.Operations.Details.Contracts;
using PlikShare.Agents.Tools;
using PlikShare.AuditLog;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;

namespace PlikShare.Agents.Operations.Details;

/// <summary>
/// Turns a pending operation's stored parameters into the tool-specific, polymorphic details the
/// approval inbox renders. Each approval-capable tool owns one branch and resolves its own params
/// (ids → names, parent folders, etc.) into its concrete <see cref="AgentOperationDetails"/> subtype.
/// </summary>
public class AgentOperationDetailsResolver(
    AuditLogService auditLogService,
    PlikShareDb plikShareDb)
{
    public AgentOperationDetails Resolve(AgentOperation operation)
    {
        return operation.ToolName switch
        {
            AgentToolNames.BulkDelete => ResolveBulkDelete(operation),
            _ => throw new InvalidOperationException(
                $"No details resolver for tool '{operation.ToolName}'.")
        };
    }

    private BulkDeleteOperationDetails ResolveBulkDelete(AgentOperation operation)
    {
        using var document = JsonDocument.Parse(operation.ParamsJson);
        var root = document.RootElement;

        var fileExternalIds = ReadStringArray(root, "fileExternalIds");

        var items = auditLogService.GetBulkItemsContext(
            folderExternalIds: ReadStringArray(root, "folderExternalIds"),
            fileExternalIds: fileExternalIds,
            fileUploadExternalIds: []);

        var parentFolderByFile = GetParentFolders(fileExternalIds);

        return new BulkDeleteOperationDetails
        {
            Folders = items.Folders
                .Select(folder => new BulkDeleteOperationDetails.FolderToDelete
                {
                    ExternalId = folder.ExternalId.Value,
                    Name = folder.Name.Encoded,
                    Path = BuildPath(folder.FolderPath)
                })
                .ToList(),

            Files = items.Files
                .Select(file => new BulkDeleteOperationDetails.FileToDelete
                {
                    ExternalId = file.ExternalId.Value,
                    FolderExternalId = parentFolderByFile.GetValueOrDefault(file.ExternalId.Value),
                    Name = $"{file.Name.Encoded}{file.Extension.Encoded}",
                    Path = BuildPath(file.FolderPath)
                })
                .ToList()
        };
    }

    private Dictionary<string, string?> GetParentFolders(List<string> fileExternalIds)
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

    private static string? BuildPath(List<Core.Encryption.EncodedMetadataValue>? path) =>
        path is null or { Count: 0 }
            ? null
            : string.Join(" / ", path.Select(segment => segment.Encoded));

    private static List<string> ReadStringArray(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var array) || array.ValueKind != JsonValueKind.Array)
            return [];

        return array
            .EnumerateArray()
            .Select(element => element.GetString())
            .Where(value => value is not null)
            .Select(value => value!)
            .ToList();
    }
}
