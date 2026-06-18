using PlikShare.Agents.Operations;
using PlikShare.AuditLog;
using PlikShare.Core.Encryption;
using PlikShare.Core.Utils;
using PlikShare.Mcp.Files.Read.Contracts;

namespace PlikShare.Mcp.Files.Read;

/// <summary>
/// Resolves a read_file operation's stored id into the file's name and path (and the byte range it
/// would read), so a human reviewing the approval sees exactly which file the agent wants to read.
/// </summary>
public class ReadFileOperationDetailsResolver(
    AuditLogService auditLogService)
{
    public ReadFileOperationDetails Resolve(AgentOperation operation)
    {
        var parameters = Json.Deserialize<ReadFileParams>(operation.ParamsJson)
            ?? throw new InvalidOperationException("The stored operation parameters were invalid.");

        var items = auditLogService.GetBulkItemsContext(
            folderExternalIds: [],
            fileExternalIds: [parameters.FileExternalId],
            fileUploadExternalIds: []);

        var file = items.Files.FirstOrDefault();

        return new ReadFileOperationDetails
        {
            FileExternalId = parameters.FileExternalId,
            Name = file is null ? null : $"{file.Name.Encoded}{file.Extension.Encoded}",
            Path = BuildPath(file?.FolderPath),
            Offset = parameters.Offset,
            MaxBytes = parameters.MaxBytes
        };
    }

    private static string? BuildPath(List<EncodedMetadataValue>? path) =>
        path is null or { Count: 0 }
            ? null
            : string.Join(" / ", path.Select(segment => segment.Encoded));
}
