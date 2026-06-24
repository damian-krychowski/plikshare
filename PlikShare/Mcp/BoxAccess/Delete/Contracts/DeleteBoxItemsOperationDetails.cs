using PlikShare.Agents.Operations.Details.Contracts;
using PlikShare.Agents.Tools;

namespace PlikShare.Mcp.BoxAccess.Delete.Contracts;

/// <summary>
/// delete_box_items deletes files and/or folders inside a box; its details carry the box (id and name)
/// and the actual folder and file names (with their paths and parent folders) so a human reviewing the
/// approval sees exactly what would be removed.
/// </summary>
public class DeleteBoxItemsOperationDetails : AgentOperationDetails
{
    public const string TypeDiscriminator = AgentToolNames.DeleteBoxItems;

    public required string BoxExternalId { get; init; }
    public required string? BoxName { get; init; }
    public required List<FolderToDelete> Folders { get; init; }
    public required List<FileToDelete> Files { get; init; }

    public class FolderToDelete
    {
        public required string ExternalId { get; init; }
        public required string Name { get; init; }
        public string? Path { get; init; }
    }

    public class FileToDelete
    {
        public required string ExternalId { get; init; }
        public required string? FolderExternalId { get; init; }
        public required string Name { get; init; }
        public string? Path { get; init; }
    }
}
