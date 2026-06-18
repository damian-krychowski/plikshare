using PlikShare.Agents.Operations;
using PlikShare.AuditLog;
using PlikShare.Core.Encryption;
using PlikShare.Core.Utils;
using PlikShare.Mcp.ShareLinks.Create.Contracts;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.Mcp.ShareLinks.Create;

/// <summary>
/// Resolves a create_share_link operation's stored ids into the names of the shared and excluded
/// folders and files, plus its settings (expiry, download limit, whether it is password protected),
/// so a human reviewing the approval sees exactly what would be shared publicly and how.
/// </summary>
public class CreateShareLinkOperationDetailsResolver(
    AuditLogService auditLogService)
{
    public CreateShareLinkOperationDetails Resolve(AgentOperation operation)
    {
        var parameters = Json.Deserialize<CreateShareLinkParams>(operation.ParamsJson)
            ?? throw new InvalidOperationException("The stored operation parameters were invalid.");

        var selected = auditLogService.GetBulkItemsContext(
            folderExternalIds: parameters.FolderExternalIds.ToList(),
            fileExternalIds: parameters.FileExternalIds.ToList(),
            fileUploadExternalIds: []);

        var excluded = auditLogService.GetBulkItemsContext(
            folderExternalIds: parameters.ExcludedFolderExternalIds.ToList(),
            fileExternalIds: parameters.ExcludedFileExternalIds.ToList(),
            fileUploadExternalIds: []);

        return new CreateShareLinkOperationDetails
        {
            Name = parameters.Name,
            SharedFolders = MapFolders(selected.Folders),
            SharedFiles = MapFiles(selected.Files),
            ExcludedFolders = MapFolders(excluded.Folders),
            ExcludedFiles = MapFiles(excluded.Files),
            ExpiresAt = parameters.ExpiresAt?.ToString("O"),
            MaxDownloads = parameters.MaxDownloads,
            HasPassword = parameters.PasswordHashBase64 is not null
        };
    }

    private static List<CreateShareLinkOperationDetails.ShareItem> MapFolders(
        List<Audit.FolderRef> folders) =>
        folders
            .Select(folder => new CreateShareLinkOperationDetails.ShareItem
            {
                ExternalId = folder.ExternalId.Value,
                Name = folder.Name.Encoded,
                Path = BuildPath(folder.FolderPath)
            })
            .ToList();

    private static List<CreateShareLinkOperationDetails.ShareItem> MapFiles(
        List<Audit.FileRef> files) =>
        files
            .Select(file => new CreateShareLinkOperationDetails.ShareItem
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
