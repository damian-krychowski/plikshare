using PlikShare.Agents.Operations.Details.Contracts;
using PlikShare.Agents.Tools;

namespace PlikShare.Mcp.Files.Rename.Contracts;

public class RenameFileOperationDetails : AgentOperationDetails
{
    public const string TypeDiscriminator = AgentToolNames.RenameFile;

    public required string FileExternalId { get; init; }
    public required string? FolderExternalId { get; init; }
    public required string? CurrentName { get; init; }
    public required string NewName { get; init; }
    public string? Path { get; init; }
}
