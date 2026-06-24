using PlikShare.Agents.Operations;
using PlikShare.Core.Utils;
using PlikShare.Mcp.BoxAccess.GetDetails.Contracts;

namespace PlikShare.Mcp.BoxAccess.GetDetails;

/// <summary>
/// Resolves a get_box_details operation's stored id into the box's current name, so a human reviewing
/// the approval sees which box would be read rather than a raw id.
/// </summary>
public class GetBoxDetailsOperationDetailsResolver(
    BoxApprovalNameResolver boxNameResolver)
{
    public GetBoxDetailsOperationDetails Resolve(AgentOperation operation)
    {
        var parameters = Json.Deserialize<GetBoxDetailsParams>(operation.ParamsJson)
            ?? throw new InvalidOperationException("The stored operation parameters were invalid.");

        return new GetBoxDetailsOperationDetails
        {
            BoxExternalId = parameters.BoxExternalId,
            BoxName = boxNameResolver.GetBoxName(parameters.BoxExternalId)
        };
    }
}
