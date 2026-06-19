using PlikShare.Agents.Operations.Details.Contracts;
using PlikShare.Agents.Tools;

namespace PlikShare.Mcp.Boxes.Create.Contracts;

/// <summary>
/// Surfaces, for a human reviewing the approval, the name and target folder of the box that would be
/// created in the workspace.
/// </summary>
public class CreateBoxOperationDetails : AgentOperationDetails
{
    public const string TypeDiscriminator = AgentToolNames.CreateBox;

    public required string WorkspaceExternalId { get; init; }
    public required string? WorkspaceName { get; init; }
    public required string Name { get; init; }
    public required string FolderExternalId { get; init; }
}
