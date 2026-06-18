using PlikShare.Agents.Operations;
using PlikShare.Mcp.Workspaces.List.Contracts;

namespace PlikShare.Mcp.Workspaces.List;

public class ListWorkspacesOperationDetailsResolver
{
    public ListWorkspacesOperationDetails Resolve(AgentOperation operation) => new();
}
