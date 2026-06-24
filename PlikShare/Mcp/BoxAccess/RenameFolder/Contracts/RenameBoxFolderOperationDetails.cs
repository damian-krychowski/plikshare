using PlikShare.Agents.Operations.Details.Contracts;
using PlikShare.Agents.Tools;

namespace PlikShare.Mcp.BoxAccess.RenameFolder.Contracts;

/// <summary>
/// rename_box_folder renames a folder inside a box; its details carry the box (id and name), the
/// folder's current name and path and the requested new name, so a human reviewing the approval sees
/// exactly what gets renamed and to what.
/// </summary>
public class RenameBoxFolderOperationDetails : AgentOperationDetails
{
    public const string TypeDiscriminator = AgentToolNames.RenameBoxFolder;

    public required string BoxExternalId { get; init; }
    public required string? BoxName { get; init; }
    public required string FolderExternalId { get; init; }
    public required string? CurrentName { get; init; }
    public required string NewName { get; init; }
    public string? Path { get; init; }
}
