namespace PlikShare.Mcp.BoxAccess.Search.Contracts;

public class SearchBoxResponseDto
{
    /// <summary>
    /// True when the search matched more files than can be returned at once. When set, Files is empty and
    /// MatchCount carries the total — narrow the phrase or search inside a specific folder.
    /// </summary>
    public required bool TooManyResults { get; init; }
    public required int MatchCount { get; init; }
    public required List<FileDto> Files { get; init; }

    public class FileDto
    {
        public required string ExternalId { get; init; }
        public required string Name { get; init; }
        public required string Extension { get; init; }
        public required long SizeInBytes { get; init; }

        /// <summary>External id of the folder that holds the file, or null when it sits at the search root.</summary>
        public required string? FolderExternalId { get; init; }
    }
}
