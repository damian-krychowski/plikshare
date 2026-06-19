using PlikShare.Agents.Operations;
using PlikShare.Core.Utils;
using PlikShare.Mcp.BoxLinks.List.Contracts;

namespace PlikShare.Mcp.BoxLinks.List;

public class ListBoxLinksOperationDetailsResolver
{
    public ListBoxLinksOperationDetails Resolve(AgentOperation operation)
    {
        var parameters = Json.Deserialize<ListBoxLinksParams>(operation.ParamsJson)
            ?? throw new InvalidOperationException("The stored operation parameters were invalid.");

        return new ListBoxLinksOperationDetails
        {
            WorkspaceExternalId = parameters.WorkspaceExternalId,
            BoxExternalId = parameters.BoxExternalId
        };
    }
}
