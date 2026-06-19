using PlikShare.Agents.Operations;
using PlikShare.Core.Utils;
using PlikShare.Mcp.Boxes.Members.List.Contracts;

namespace PlikShare.Mcp.Boxes.Members.List;

public class ListBoxMembersOperationDetailsResolver
{
    public ListBoxMembersOperationDetails Resolve(AgentOperation operation)
    {
        var parameters = Json.Deserialize<ListBoxMembersParams>(operation.ParamsJson)
            ?? throw new InvalidOperationException("The stored operation parameters were invalid.");

        return new ListBoxMembersOperationDetails
        {
            WorkspaceExternalId = parameters.WorkspaceExternalId,
            BoxExternalId = parameters.BoxExternalId
        };
    }
}
