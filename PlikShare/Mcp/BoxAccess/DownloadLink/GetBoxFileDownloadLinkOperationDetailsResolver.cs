using PlikShare.Agents.Operations;
using PlikShare.AuditLog;
using PlikShare.Core.Encryption;
using PlikShare.Core.Utils;
using PlikShare.Mcp.BoxAccess.DownloadLink.Contracts;

namespace PlikShare.Mcp.BoxAccess.DownloadLink;

/// <summary>
/// Resolves a get_box_file_download_link operation's stored id into the box name and the file's name and
/// path (and the requested link lifetime), so a human reviewing the approval sees exactly which file
/// would get a public download link, and for how long.
/// </summary>
public class GetBoxFileDownloadLinkOperationDetailsResolver(
    BoxApprovalNameResolver boxNameResolver,
    AuditLogService auditLogService)
{
    public GetBoxFileDownloadLinkOperationDetails Resolve(AgentOperation operation)
    {
        var parameters = Json.Deserialize<GetBoxFileDownloadLinkParams>(operation.ParamsJson)
            ?? throw new InvalidOperationException("The stored operation parameters were invalid.");

        var items = auditLogService.GetBulkItemsContext(
            folderExternalIds: [],
            fileExternalIds: [parameters.FileExternalId],
            fileUploadExternalIds: []);

        var file = items.Files.FirstOrDefault();

        return new GetBoxFileDownloadLinkOperationDetails
        {
            BoxExternalId = parameters.BoxExternalId,
            BoxName = boxNameResolver.GetBoxName(parameters.BoxExternalId),
            FileExternalId = parameters.FileExternalId,
            Name = file is null ? null : $"{file.Name.Encoded}{file.Extension.Encoded}",
            Path = BuildPath(file?.FolderPath),
            ExpiresInMinutes = parameters.ExpiresInMinutes
        };
    }

    private static string? BuildPath(List<EncodedMetadataValue>? path) =>
        path is null or { Count: 0 }
            ? null
            : string.Join(" / ", path.Select(segment => segment.Encoded));
}
