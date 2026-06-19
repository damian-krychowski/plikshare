using PlikShare.Agents.Operations;
using PlikShare.Core.Utils;
using PlikShare.Mcp.Workspaces.Members.List.Contracts;

namespace PlikShare.Mcp.Workspaces.Members.List;

public class ListWorkspaceMembersOperationDetailsResolver
{
    public ListWorkspaceMembersOperationDetails Resolve(AgentOperation operation)
    {
        var parameters = Json.Deserialize<ListWorkspaceMembersParams>(operation.ParamsJson)
            ?? throw new InvalidOperationException("The stored operation parameters were invalid.");

        return new ListWorkspaceMembersOperationDetails
        {
            WorkspaceExternalId = parameters.WorkspaceExternalId
        };
    }
}
