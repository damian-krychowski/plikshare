using PlikShare.Agents.Operations;
using PlikShare.Mcp.BoxAccess.List.Contracts;

namespace PlikShare.Mcp.BoxAccess.List;

public class ListBoxesOperationDetailsResolver
{
    public ListBoxesOperationDetails Resolve(AgentOperation operation) => new();
}
