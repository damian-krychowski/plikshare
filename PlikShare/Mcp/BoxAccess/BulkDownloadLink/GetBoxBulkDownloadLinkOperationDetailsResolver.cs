using PlikShare.Agents.Operations;
using PlikShare.AuditLog;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using PlikShare.Mcp.BoxAccess.BulkDownloadLink.Contracts;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.Mcp.BoxAccess.BulkDownloadLink;

/// <summary>
/// Resolves a get_box_bulk_download_link operation's stored ids into the box name and the names and
/// paths of the folders and files that would be bundled into the ZIP (and any carved out), plus the
/// link's lifetime, so a human reviewing the approval sees exactly what would be downloadable.
/// </summary>
public class GetBoxBulkDownloadLinkOperationDetailsResolver(
    BoxApprovalNameResolver boxNameResolver,
    AuditLogService auditLogService,
    PlikShareDb plikShareDb)
{
    public GetBoxBulkDownloadLinkOperationDetails Resolve(AgentOperation operation)
    {
        var parameters = Json.Deserialize<GetBoxBulkDownloadLinkParams>(operation.ParamsJson)
            ?? throw new InvalidOperationException("The stored operation parameters were invalid.");

        var selected = auditLogService.GetBulkItemsContext(
            folderExternalIds: parameters.FolderExternalIds.ToList(),
            fileExternalIds: parameters.FileExternalIds.ToList(),
            fileUploadExternalIds: []);

        var excluded = auditLogService.GetBulkItemsContext(
            folderExternalIds: parameters.ExcludedFolderExternalIds.ToList(),
            fileExternalIds: parameters.ExcludedFileExternalIds.ToList(),
            fileUploadExternalIds: []);

        var parentFolderByFile = GetParentFolders(
            parameters.FileExternalIds
                .Concat(parameters.ExcludedFileExternalIds)
                .ToList());

        return new GetBoxBulkDownloadLinkOperationDetails
        {
            BoxExternalId = parameters.BoxExternalId,
            BoxName = boxNameResolver.GetBoxName(parameters.BoxExternalId),
            Folders = MapFolders(selected.Folders),
            Files = MapFiles(selected.Files, parentFolderByFile),
            ExcludedFolders = MapFolders(excluded.Folders),
            ExcludedFiles = MapFiles(excluded.Files, parentFolderByFile),
            ExpiresInMinutes = parameters.ExpiresInMinutes
        };
    }

    private static List<GetBoxBulkDownloadLinkOperationDetails.DownloadItem> MapFolders(
        List<Audit.FolderRef> folders) =>
        folders
            .Select(folder => new GetBoxBulkDownloadLinkOperationDetails.DownloadItem
            {
                ExternalId = folder.ExternalId.Value,
                Name = folder.Name.Encoded,
                Path = BuildPath(folder.FolderPath)
            })
            .ToList();

    private static List<GetBoxBulkDownloadLinkOperationDetails.DownloadItem> MapFiles(
        List<Audit.FileRef> files,
        Dictionary<string, string?> parentFolderByFile) =>
        files
            .Select(file => new GetBoxBulkDownloadLinkOperationDetails.DownloadItem
            {
                ExternalId = file.ExternalId.Value,
                Name = $"{file.Name.Encoded}{file.Extension.Encoded}",
                Path = BuildPath(file.FolderPath),
                FolderExternalId = parentFolderByFile.GetValueOrDefault(file.ExternalId.Value)
            })
            .ToList();

    private Dictionary<string, string?> GetParentFolders(IReadOnlyList<string> fileExternalIds)
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

    private static string? BuildPath(List<EncodedMetadataValue>? path) =>
        path is null or { Count: 0 }
            ? null
            : string.Join(" / ", path.Select(segment => segment.Encoded));
}
