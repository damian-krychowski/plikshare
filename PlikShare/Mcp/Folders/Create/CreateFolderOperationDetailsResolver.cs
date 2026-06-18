using PlikShare.Agents.Operations;
using PlikShare.AuditLog;
using PlikShare.Core.Encryption;
using PlikShare.Core.Utils;
using PlikShare.Mcp.Folders.Create.Contracts;

namespace PlikShare.Mcp.Folders.Create;

/// <summary>
/// Resolves a create_folder operation's stored parameters into the new folder's name and the full
/// location of its parent, so a human reviewing the approval sees what gets created and where.
/// </summary>
public class CreateFolderOperationDetailsResolver(
    AuditLogService auditLogService)
{
    public CreateFolderOperationDetails Resolve(AgentOperation operation)
    {
        var parameters = Json.Deserialize<CreateFolderParams>(operation.ParamsJson)
            ?? throw new InvalidOperationException("The stored operation parameters were invalid.");

        return new CreateFolderOperationDetails
        {
            Name = parameters.Name,
            ParentFolderExternalId = parameters.ParentFolderExternalId,
            ParentLocation = ResolveParentLocation(parameters.ParentFolderExternalId)
        };
    }

    private string? ResolveParentLocation(string? parentFolderExternalId)
    {
        if (string.IsNullOrWhiteSpace(parentFolderExternalId))
            return null;

        var items = auditLogService.GetBulkItemsContext(
            folderExternalIds: [parentFolderExternalId],
            fileExternalIds: [],
            fileUploadExternalIds: []);

        var parent = items.Folders.FirstOrDefault();

        if (parent is null)
            return null;

        var ancestorPath = BuildPath(parent.FolderPath);

        return ancestorPath is null
            ? parent.Name.Encoded
            : $"{ancestorPath} / {parent.Name.Encoded}";
    }

    private static string? BuildPath(List<EncodedMetadataValue>? path) =>
        path is null or { Count: 0 }
            ? null
            : string.Join(" / ", path.Select(segment => segment.Encoded));
}
