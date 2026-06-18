using PlikShare.Agents.Operations;
using PlikShare.AuditLog;
using PlikShare.Core.Encryption;
using PlikShare.Core.Utils;
using PlikShare.Mcp.Files.BulkDownloadLink.Contracts;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.Mcp.Files.BulkDownloadLink;

/// <summary>
/// Resolves a get_bulk_download_link operation's stored ids into the names and paths of the folders and
/// files that would be bundled into the ZIP (and any carved out), plus the link's lifetime, so a human
/// reviewing the approval sees exactly what would be downloadable and for how long.
/// </summary>
public class GetBulkDownloadLinkOperationDetailsResolver(
    AuditLogService auditLogService)
{
    public GetBulkDownloadLinkOperationDetails Resolve(AgentOperation operation)
    {
        var parameters = Json.Deserialize<GetBulkDownloadLinkParams>(operation.ParamsJson)
            ?? throw new InvalidOperationException("The stored operation parameters were invalid.");

        var selected = auditLogService.GetBulkItemsContext(
            folderExternalIds: parameters.FolderExternalIds.ToList(),
            fileExternalIds: parameters.FileExternalIds.ToList(),
            fileUploadExternalIds: []);

        var excluded = auditLogService.GetBulkItemsContext(
            folderExternalIds: parameters.ExcludedFolderExternalIds.ToList(),
            fileExternalIds: parameters.ExcludedFileExternalIds.ToList(),
            fileUploadExternalIds: []);

        return new GetBulkDownloadLinkOperationDetails
        {
            Folders = MapFolders(selected.Folders),
            Files = MapFiles(selected.Files),
            ExcludedFolders = MapFolders(excluded.Folders),
            ExcludedFiles = MapFiles(excluded.Files),
            ExpiresInMinutes = parameters.ExpiresInMinutes
        };
    }

    private static List<GetBulkDownloadLinkOperationDetails.DownloadItem> MapFolders(
        List<Audit.FolderRef> folders) =>
        folders
            .Select(folder => new GetBulkDownloadLinkOperationDetails.DownloadItem
            {
                ExternalId = folder.ExternalId.Value,
                Name = folder.Name.Encoded,
                Path = BuildPath(folder.FolderPath)
            })
            .ToList();

    private static List<GetBulkDownloadLinkOperationDetails.DownloadItem> MapFiles(
        List<Audit.FileRef> files) =>
        files
            .Select(file => new GetBulkDownloadLinkOperationDetails.DownloadItem
            {
                ExternalId = file.ExternalId.Value,
                Name = $"{file.Name.Encoded}{file.Extension.Encoded}",
                Path = BuildPath(file.FolderPath)
            })
            .ToList();

    private static string? BuildPath(List<EncodedMetadataValue>? path) =>
        path is null or { Count: 0 }
            ? null
            : string.Join(" / ", path.Select(segment => segment.Encoded));
}
