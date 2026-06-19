using PlikShare.Agents.Operations;
using PlikShare.Core.Utils;
using PlikShare.Mcp.Boxes.Get.Contracts;

namespace PlikShare.Mcp.Boxes.Get;

public class GetBoxOperationDetailsResolver
{
    public GetBoxOperationDetails Resolve(AgentOperation operation)
    {
        var parameters = Json.Deserialize<GetBoxParams>(operation.ParamsJson)
            ?? throw new InvalidOperationException("The stored operation parameters were invalid.");

        return new GetBoxOperationDetails
        {
            WorkspaceExternalId = parameters.WorkspaceExternalId,
            BoxExternalId = parameters.BoxExternalId
        };
    }
}
