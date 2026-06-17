using System.Text.Json.Serialization;

namespace PlikShare.Agents.Operations.Details.Contracts;

[JsonDerivedType(derivedType: typeof(BulkDeleteOperationDetails), typeDiscriminator: BulkDeleteOperationDetails.TypeDiscriminator)]
public abstract class AgentOperationDetails
{
}
