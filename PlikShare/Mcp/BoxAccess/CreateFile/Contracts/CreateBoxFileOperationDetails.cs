using PlikShare.Agents.Operations.Details.Contracts;
using PlikShare.Agents.Tools;

namespace PlikShare.Mcp.BoxAccess.CreateFile.Contracts;

/// <summary>
/// create_box_file creates a text file inside a box; its details carry the box (id and name), the new
/// file's name, the full location of its parent folder, the content size and a preview of the content,
/// so a human reviewing the approval sees what gets written and where.
/// </summary>
public class CreateBoxFileOperationDetails : AgentOperationDetails
{
    public const string TypeDiscriminator = AgentToolNames.CreateBoxFile;

    public required string BoxExternalId { get; init; }
    public required string? BoxName { get; init; }
    public required string Name { get; init; }
    public required string? FolderExternalId { get; init; }
    public required string? ParentLocation { get; init; }
    public required int SizeInBytes { get; init; }
    public required string ContentPreview { get; init; }
    public required bool IsPreviewTruncated { get; init; }
}
