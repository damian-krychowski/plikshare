using PlikShare.Agents.Operations.Details.Contracts;
using PlikShare.Agents.Tools;

namespace PlikShare.Mcp.BoxAccess.RenameFile.Contracts;

/// <summary>
/// rename_box_file renames a file inside a box; its details carry the box (id and name), the file's
/// current name (with extension), its parent folder and path, and the requested new name, so a human
/// reviewing the approval sees exactly what gets renamed and to what — including the extension that is
/// kept.
/// </summary>
public class RenameBoxFileOperationDetails : AgentOperationDetails
{
    public const string TypeDiscriminator = AgentToolNames.RenameBoxFile;

    public required string BoxExternalId { get; init; }
    public required string? BoxName { get; init; }
    public required string FileExternalId { get; init; }
    public required string? FolderExternalId { get; init; }
    public required string? CurrentName { get; init; }
    public required string NewName { get; init; }
    public string? Path { get; init; }
}
