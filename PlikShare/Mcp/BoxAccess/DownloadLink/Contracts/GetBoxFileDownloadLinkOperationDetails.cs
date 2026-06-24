using PlikShare.Agents.Operations.Details.Contracts;
using PlikShare.Agents.Tools;

namespace PlikShare.Mcp.BoxAccess.DownloadLink.Contracts;

/// <summary>
/// get_box_file_download_link mints a short-lived download link for a file inside a box; its details
/// carry the box (id and name), the file's name and path and the requested link lifetime, so a human
/// reviewing the approval sees exactly which file would be exposed, and for how long.
/// </summary>
public class GetBoxFileDownloadLinkOperationDetails : AgentOperationDetails
{
    public const string TypeDiscriminator = AgentToolNames.GetBoxFileDownloadLink;

    public required string BoxExternalId { get; init; }
    public required string? BoxName { get; init; }
    public required string FileExternalId { get; init; }
    public required string? Name { get; init; }
    public required string? Path { get; init; }
    public required int? ExpiresInMinutes { get; init; }
}
