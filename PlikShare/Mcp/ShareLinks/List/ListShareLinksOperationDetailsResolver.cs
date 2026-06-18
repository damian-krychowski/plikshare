using PlikShare.Agents.Operations;
using PlikShare.Mcp.ShareLinks.List.Contracts;

namespace PlikShare.Mcp.ShareLinks.List;

public class ListShareLinksOperationDetailsResolver
{
    public ListShareLinksOperationDetails Resolve(AgentOperation operation) => new();
}
