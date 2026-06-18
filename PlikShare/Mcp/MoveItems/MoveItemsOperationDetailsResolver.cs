using PlikShare.Agents.Operations;
using PlikShare.AuditLog;
using PlikShare.Core.Encryption;
using PlikShare.Core.Utils;
using PlikShare.Files.Id;
using PlikShare.Folders.Id;
using PlikShare.Mcp.MoveItems.Contracts;

namespace PlikShare.Mcp.MoveItems;

/// <summary>
/// Resolves a move_items operation's stored ids into the names and paths of the folders and files
/// being moved and the destination folder, so a human reviewing the approval sees exactly what gets
/// moved and where.
/// </summary>
public class MoveItemsOperationDetailsResolver(
    AuditLogService auditLogService)
{
    public MoveItemsOperationDetails Resolve(AgentOperation operation)
    {
        var parameters = Json.Deserialize<MoveItemsParams>(operation.ParamsJson)
            ?? throw new InvalidOperationException("The stored operation parameters were invalid.");

        var destination = string.IsNullOrWhiteSpace(parameters.DestinationFolderExternalId)
            ? (FolderExtId?)null
            : FolderExtId.Parse(parameters.DestinationFolderExternalId);

        var context = auditLogService.GetItemsMovedContext(
            destinationFolderExternalId: destination,
            folderExternalIds: parameters.FolderExternalIds.Select(FolderExtId.Parse).ToList(),
            fileExternalIds: parameters.FileExternalIds.Select(FileExtId.Parse).ToList(),
            fileUploadExternalIds: []);

        return new MoveItemsOperationDetails
        {
            DestinationFolderExternalId = parameters.DestinationFolderExternalId,
            DestinationName = context.DestinationFolder?.Name.Encoded,
            DestinationPath = BuildPath(context.DestinationFolder?.FolderPath),

            Folders = context.Folders
                .Select(folder => new MoveItemsOperationDetails.ItemToMove
                {
                    ExternalId = folder.ExternalId.Value,
                    Name = folder.Name.Encoded,
                    Path = BuildPath(folder.FolderPath)
                })
                .ToList(),

            Files = context.Files
                .Select(file => new MoveItemsOperationDetails.ItemToMove
                {
                    ExternalId = file.ExternalId.Value,
                    Name = $"{file.Name.Encoded}{file.Extension.Encoded}",
                    Path = BuildPath(file.FolderPath)
                })
                .ToList()
        };
    }

    private static string? BuildPath(List<EncodedMetadataValue>? path) =>
        path is null or { Count: 0 }
            ? null
            : string.Join(" / ", path.Select(segment => segment.Encoded));
}
