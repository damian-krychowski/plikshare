using PlikShare.Agents.Operations;
using PlikShare.AuditLog;
using PlikShare.Core.Encryption;
using PlikShare.Core.Utils;
using PlikShare.Mcp.Files.Get.Contracts;

namespace PlikShare.Mcp.Files.Get;

/// <summary>
/// Resolves a get_file operation's stored id into the file's name and path, so a human reviewing the
/// approval sees exactly which file's details the agent wants to read.
/// </summary>
public class GetFileOperationDetailsResolver(
    AuditLogService auditLogService)
{
    public GetFileOperationDetails Resolve(AgentOperation operation)
    {
        var parameters = Json.Deserialize<GetFileParams>(operation.ParamsJson)
            ?? throw new InvalidOperationException("The stored operation parameters were invalid.");

        var items = auditLogService.GetBulkItemsContext(
            folderExternalIds: [],
            fileExternalIds: [parameters.FileExternalId],
            fileUploadExternalIds: []);

        var file = items.Files.FirstOrDefault();

        return new GetFileOperationDetails
        {
            FileExternalId = parameters.FileExternalId,
            Name = file is null ? null : $"{file.Name.Encoded}{file.Extension.Encoded}",
            Path = BuildPath(file?.FolderPath)
        };
    }

    private static string? BuildPath(List<EncodedMetadataValue>? path) =>
        path is null or { Count: 0 }
            ? null
            : string.Join(" / ", path.Select(segment => segment.Encoded));
}
