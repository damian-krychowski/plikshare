using PlikShare.Agents.Operations.Details.Contracts;
using PlikShare.Agents.Tools;

namespace PlikShare.Mcp.BoxAccess.CreateFolder.Contracts;

/// <summary>
/// create_box_folder creates a folder inside a box; its details carry the box (id and name), the new
/// folder's name and the full location of its parent, so a human reviewing the approval sees what gets
/// created and where.
/// </summary>
public class CreateBoxFolderOperationDetails : AgentOperationDetails
{
    public const string TypeDiscriminator = AgentToolNames.CreateBoxFolder;

    public required string BoxExternalId { get; init; }
    public required string? BoxName { get; init; }
    public required string Name { get; init; }
    public required string? ParentFolderExternalId { get; init; }
    public required string? ParentLocation { get; init; }
}
