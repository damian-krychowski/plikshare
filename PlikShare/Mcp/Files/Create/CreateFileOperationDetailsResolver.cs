using System.Text;
using PlikShare.Agents.Operations;
using PlikShare.AuditLog;
using PlikShare.Core.Encryption;
using PlikShare.Core.Utils;
using PlikShare.Mcp.Files.Create.Contracts;

namespace PlikShare.Mcp.Files.Create;

/// <summary>
/// Resolves a create_file operation's stored parameters into the new file's name, the full location
/// of its parent folder, the content size and a preview of the content, so a human reviewing the
/// approval sees what gets written and where.
/// </summary>
public class CreateFileOperationDetailsResolver(
    AuditLogService auditLogService)
{
    private const int ContentPreviewLimit = 2000;

    public CreateFileOperationDetails Resolve(AgentOperation operation)
    {
        var parameters = Json.Deserialize<CreateFileParams>(operation.ParamsJson)
            ?? throw new InvalidOperationException("The stored operation parameters were invalid.");

        var content = parameters.Content ?? string.Empty;
        var isTruncated = content.Length > ContentPreviewLimit;

        return new CreateFileOperationDetails
        {
            Name = parameters.Name,
            FolderExternalId = parameters.FolderExternalId,
            ParentLocation = ResolveParentLocation(parameters.FolderExternalId),
            SizeInBytes = Encoding.UTF8.GetByteCount(content),
            ContentPreview = isTruncated ? content[..ContentPreviewLimit] : content,
            IsPreviewTruncated = isTruncated
        };
    }

    private string? ResolveParentLocation(string? folderExternalId)
    {
        if (string.IsNullOrWhiteSpace(folderExternalId))
            return null;

        var items = auditLogService.GetBulkItemsContext(
            folderExternalIds: [folderExternalId],
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
