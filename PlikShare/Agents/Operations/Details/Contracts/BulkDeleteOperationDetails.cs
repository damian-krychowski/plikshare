using PlikShare.Agents.Tools;

namespace PlikShare.Agents.Operations.Details.Contracts;

public class BulkDeleteOperationDetails : AgentOperationDetails
{
    public const string TypeDiscriminator = AgentToolNames.BulkDelete;

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
