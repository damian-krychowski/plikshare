using PlikShare.Agents.Operations.Details.Contracts;
using PlikShare.Agents.Tools;

namespace PlikShare.Mcp.Files.Read.Contracts;

public class ReadFileOperationDetails : AgentOperationDetails
{
    public const string TypeDiscriminator = AgentToolNames.ReadFile;

    public required string FileExternalId { get; init; }
    public required string? Name { get; init; }
    public required string? Path { get; init; }
    public required long Offset { get; init; }
    public required int? MaxBytes { get; init; }
}
