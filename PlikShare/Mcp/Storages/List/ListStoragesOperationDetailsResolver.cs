using PlikShare.Agents.Operations;
using PlikShare.Mcp.Storages.List.Contracts;

namespace PlikShare.Mcp.Storages.List;

public class ListStoragesOperationDetailsResolver
{
    public ListStoragesOperationDetails Resolve(AgentOperation operation) => new();
}
