namespace PlikShare.MediaProcessing.Generation.Contracts;

public class CountThumbnailableFilesRequestDto
{
    public required List<string> SelectedFolders { get; init; } = [];
    public required List<string> SelectedFiles { get; init; } = [];
    public required List<string> ExcludedFolders { get; init; } = [];
    public required List<string> ExcludedFiles { get; init; } = [];
}

public class CountThumbnailableFilesResponseDto
{
    public required int FileCount { get; init; }
    public required long TotalSizeInBytes { get; init; }
}
