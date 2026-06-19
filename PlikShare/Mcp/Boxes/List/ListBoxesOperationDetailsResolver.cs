using PlikShare.Agents.Operations;
using PlikShare.Core.Utils;
using PlikShare.Mcp.Boxes.List.Contracts;

namespace PlikShare.Mcp.Boxes.List;

public class ListBoxesOperationDetailsResolver
{
    public ListBoxesOperationDetails Resolve(AgentOperation operation)
    {
        var parameters = Json.Deserialize<ListBoxesParams>(operation.ParamsJson)
            ?? throw new InvalidOperationException("The stored operation parameters were invalid.");

        return new ListBoxesOperationDetails
        {
            WorkspaceExternalId = parameters.WorkspaceExternalId
        };
    }
}
