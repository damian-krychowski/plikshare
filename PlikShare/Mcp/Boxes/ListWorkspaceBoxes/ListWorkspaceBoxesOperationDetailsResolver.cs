using PlikShare.Agents.Operations;
using PlikShare.Core.Utils;
using PlikShare.Mcp.Boxes.ListWorkspaceBoxes.Contracts;

namespace PlikShare.Mcp.Boxes.ListWorkspaceBoxes;

public class ListWorkspaceBoxesOperationDetailsResolver
{
    public ListWorkspaceBoxesOperationDetails Resolve(AgentOperation operation)
    {
        var parameters = Json.Deserialize<ListWorkspaceBoxesParams>(operation.ParamsJson)
            ?? throw new InvalidOperationException("The stored operation parameters were invalid.");

        return new ListWorkspaceBoxesOperationDetails
        {
            WorkspaceExternalId = parameters.WorkspaceExternalId
        };
    }
}
