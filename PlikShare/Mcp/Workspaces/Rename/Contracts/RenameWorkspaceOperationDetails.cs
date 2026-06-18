using PlikShare.Agents.Operations.Details.Contracts;
using PlikShare.Agents.Tools;

namespace PlikShare.Mcp.Workspaces.Rename.Contracts;

public class RenameWorkspaceOperationDetails : AgentOperationDetails
{
    public const string TypeDiscriminator = AgentToolNames.RenameWorkspace;

    public required string WorkspaceExternalId { get; init; }
    public required string? CurrentName { get; init; }
    public required string NewName { get; init; }
}
