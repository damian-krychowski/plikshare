using PlikShare.Agents.Operations.Details.Contracts;
using PlikShare.Agents.Tools;

namespace PlikShare.Mcp.Workspaces.Content.Contracts;

public class ListWorkspaceContentOperationDetails : AgentOperationDetails
{
    public const string TypeDiscriminator = AgentToolNames.ListWorkspaceContent;

    public required string? FolderExternalId { get; init; }
    public required string? FolderName { get; init; }
    public required string? Type { get; init; }
}
