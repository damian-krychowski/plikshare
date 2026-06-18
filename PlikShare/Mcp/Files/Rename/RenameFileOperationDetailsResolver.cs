using PlikShare.Agents.Operations;
using PlikShare.AuditLog;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using PlikShare.Mcp.Files.Rename.Contracts;

namespace PlikShare.Mcp.Files.Rename;

/// <summary>
/// Resolves a rename_file operation's stored ids into the file's current name (with extension), its
/// parent folder and path, and the requested new name, so a human reviewing the approval sees exactly
/// what gets renamed and to what — including the extension that is kept.
/// </summary>
public class RenameFileOperationDetailsResolver(
    AuditLogService auditLogService,
    PlikShareDb plikShareDb)
{
    public RenameFileOperationDetails Resolve(AgentOperation operation)
    {
        var parameters = Json.Deserialize<RenameFileParams>(operation.ParamsJson)
            ?? throw new InvalidOperationException("The stored operation parameters were invalid.");

        var items = auditLogService.GetBulkItemsContext(
            folderExternalIds: [],
            fileExternalIds: [parameters.FileExternalId],
            fileUploadExternalIds: []);

        var file = items.Files.FirstOrDefault();
        var extension = file?.Extension.Encoded ?? string.Empty;

        return new RenameFileOperationDetails
        {
            FileExternalId = parameters.FileExternalId,
            FolderExternalId = GetParentFolderExternalId(parameters.FileExternalId),
            CurrentName = file is null ? null : $"{file.Name.Encoded}{extension}",
            NewName = $"{parameters.Name}{extension}",
            Path = BuildPath(file?.FolderPath)
        };
    }

    private string? GetParentFolderExternalId(string fileExternalId)
    {
        using var connection = plikShareDb.OpenConnection();

        var result = connection
            .OneRowCmd(
                sql: """
                     SELECT parent.fo_external_id
                     FROM fi_files AS fi
                     LEFT JOIN fo_folders AS parent ON fi.fi_folder_id = parent.fo_id
                     WHERE fi.fi_external_id = $externalId
                     LIMIT 1
                     """,
                readRowFunc: reader => reader.GetStringOrNull(0))
            .WithParameter("$externalId", fileExternalId)
            .Execute();

        return result.IsEmpty ? null : result.Value;
    }

    private static string? BuildPath(List<EncodedMetadataValue>? path) =>
        path is null or { Count: 0 }
            ? null
            : string.Join(" / ", path.Select(segment => segment.Encoded));
}
