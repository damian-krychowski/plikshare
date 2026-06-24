namespace PlikShare.Mcp.BoxAccess.Content.Contracts;

public class ListBoxContentResponseDto
{
    /// <summary>External id of the folder that was listed (the box root when no folder was requested).</summary>
    public required string FolderExternalId { get; init; }
    public required List<FolderDto> Folders { get; init; }
    public required List<FileDto> Files { get; init; }

    public class FolderDto
    {
        public required string ExternalId { get; init; }
        public required string Name { get; init; }
    }

    public class FileDto
    {
        public required string ExternalId { get; init; }
        public required string Name { get; init; }
        public required string Extension { get; init; }
        public required long SizeInBytes { get; init; }
    }
}
