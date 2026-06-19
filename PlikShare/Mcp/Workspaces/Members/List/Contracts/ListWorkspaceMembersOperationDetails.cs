using PlikShare.Agents.Operations.Details.Contracts;
using PlikShare.Agents.Tools;

namespace PlikShare.Mcp.Workspaces.Members.List.Contracts;

/// <summary>
/// list_workspace_members reads the members of a single workspace; its details carry the workspace id so
/// a human reviewing the approval sees which workspace's membership would be read.
/// </summary>
public class ListWorkspaceMembersOperationDetails : AgentOperationDetails
{
    public const string TypeDiscriminator = AgentToolNames.ListWorkspaceMembers;

    public required string WorkspaceExternalId { get; init; }
}
