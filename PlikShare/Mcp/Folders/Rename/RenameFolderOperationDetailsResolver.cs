using PlikShare.Agents.Operations;
using PlikShare.AuditLog;
using PlikShare.Core.Encryption;
using PlikShare.Core.Utils;
using PlikShare.Mcp.Folders.Rename.Contracts;

namespace PlikShare.Mcp.Folders.Rename;

/// <summary>
/// Resolves a rename_folder operation's stored ids into the folder's current name, path and the
/// requested new name, so a human reviewing the approval sees exactly what gets renamed and to what.
/// </summary>
public class RenameFolderOperationDetailsResolver(
    AuditLogService auditLogService)
{
    public RenameFolderOperationDetails Resolve(AgentOperation operation)
    {
        var parameters = Json.Deserialize<RenameFolderParams>(operation.ParamsJson)
            ?? throw new InvalidOperationException("The stored operation parameters were invalid.");

        var items = auditLogService.GetBulkItemsContext(
            folderExternalIds: [parameters.FolderExternalId],
            fileExternalIds: [],
            fileUploadExternalIds: []);

        var folder = items.Folders.FirstOrDefault();

        return new RenameFolderOperationDetails
        {
            FolderExternalId = parameters.FolderExternalId,
            CurrentName = folder?.Name.Encoded,
            NewName = parameters.Name,
            Path = BuildPath(folder?.FolderPath)
        };
    }

    private static string? BuildPath(List<EncodedMetadataValue>? path) =>
        path is null or { Count: 0 }
            ? null
            : string.Join(" / ", path.Select(segment => segment.Encoded));
}
