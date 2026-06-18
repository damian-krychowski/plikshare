using PlikShare.Agents.Operations.Details.Contracts;
using PlikShare.Agents.Tools;

namespace PlikShare.Mcp.Files.Get.Contracts;

public class GetFileOperationDetails : AgentOperationDetails
{
    public const string TypeDiscriminator = AgentToolNames.GetFile;

    public required string FileExternalId { get; init; }
    public required string? Name { get; init; }
    public required string? Path { get; init; }
}
