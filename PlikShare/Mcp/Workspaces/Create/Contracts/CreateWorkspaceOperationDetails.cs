using PlikShare.Agents.Operations.Details.Contracts;
using PlikShare.Agents.Tools;

namespace PlikShare.Mcp.Workspaces.Create.Contracts;

public class CreateWorkspaceOperationDetails : AgentOperationDetails
{
    public const string TypeDiscriminator = AgentToolNames.CreateWorkspace;

    public required string Name { get; init; }
    public required string StorageExternalId { get; init; }
    public required string? StorageName { get; init; }
}
