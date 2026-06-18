using PlikShare.Agents.Operations.Details.Contracts;
using PlikShare.Agents.Tools;

namespace PlikShare.Mcp.Files.Create.Contracts;

public class CreateFileOperationDetails : AgentOperationDetails
{
    public const string TypeDiscriminator = AgentToolNames.CreateFile;

    public required string Name { get; init; }
    public required string? FolderExternalId { get; init; }
    public required string? ParentLocation { get; init; }
    public required int SizeInBytes { get; init; }
    public required string ContentPreview { get; init; }
    public required bool IsPreviewTruncated { get; init; }
}
