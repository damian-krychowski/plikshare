namespace PlikShare.Mcp.Boxes.Get.Contracts;

public class GetBoxResponseDto
{
    public required string ExternalId { get; init; }
    public required string Name { get; init; }
    public required bool IsEnabled { get; init; }
    public required List<FolderPathItemDto> FolderPath { get; init; }
    public required int MembersCount { get; init; }
    public required int LinksCount { get; init; }
    public required List<SubfolderDto>? Subfolders { get; init; }
    public required List<FileDto>? Files { get; init; }

    public class FolderPathItemDto
    {
        public required string ExternalId { get; init; }
        public required string Name { get; init; }
    }

    public class SubfolderDto
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
