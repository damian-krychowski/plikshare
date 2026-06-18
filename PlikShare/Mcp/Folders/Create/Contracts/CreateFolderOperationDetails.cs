using PlikShare.Agents.Operations.Details.Contracts;
using PlikShare.Agents.Tools;

namespace PlikShare.Mcp.Folders.Create.Contracts;

public class CreateFolderOperationDetails : AgentOperationDetails
{
    public const string TypeDiscriminator = AgentToolNames.CreateFolder;

    public required string Name { get; init; }
    public required string? ParentFolderExternalId { get; init; }
    public required string? ParentLocation { get; init; }
}
