using PlikShare.Agents.Operations.Details.Contracts;
using PlikShare.Agents.Tools;

namespace PlikShare.Mcp.Folders.Rename.Contracts;

public class RenameFolderOperationDetails : AgentOperationDetails
{
    public const string TypeDiscriminator = AgentToolNames.RenameFolder;

    public required string FolderExternalId { get; init; }
    public required string? CurrentName { get; init; }
    public required string NewName { get; init; }
    public string? Path { get; init; }
}
