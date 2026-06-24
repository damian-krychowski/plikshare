using PlikShare.Agents.Operations;
using PlikShare.AuditLog;
using PlikShare.Core.Encryption;
using PlikShare.Core.Utils;
using PlikShare.Mcp.BoxAccess.RenameFolder.Contracts;

namespace PlikShare.Mcp.BoxAccess.RenameFolder;

/// <summary>
/// Resolves a rename_box_folder operation's stored ids into the box name, the folder's current name and
/// path and the requested new name, so a human reviewing the approval sees exactly what gets renamed and
/// to what.
/// </summary>
public class RenameBoxFolderOperationDetailsResolver(
    BoxApprovalNameResolver boxNameResolver,
    AuditLogService auditLogService)
{
    public RenameBoxFolderOperationDetails Resolve(AgentOperation operation)
    {
        var parameters = Json.Deserialize<RenameBoxFolderParams>(operation.ParamsJson)
            ?? throw new InvalidOperationException("The stored operation parameters were invalid.");

        var items = auditLogService.GetBulkItemsContext(
            folderExternalIds: [parameters.FolderExternalId],
            fileExternalIds: [],
            fileUploadExternalIds: []);

        var folder = items.Folders.FirstOrDefault();

        return new RenameBoxFolderOperationDetails
        {
            BoxExternalId = parameters.BoxExternalId,
            BoxName = boxNameResolver.GetBoxName(parameters.BoxExternalId),
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
