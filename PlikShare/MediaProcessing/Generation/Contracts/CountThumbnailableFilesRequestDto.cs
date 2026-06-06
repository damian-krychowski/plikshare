using ProtoBuf;

namespace PlikShare.MediaProcessing.Generation.Contracts;

[ProtoContract]
public class CountThumbnailableFilesRequestDto
{
    [ProtoMember(1)]
    public required List<string> SelectedFolders { get; init; } = [];

    [ProtoMember(2)]
    public required List<string> SelectedFiles { get; init; } = [];

    [ProtoMember(3)]
    public required List<string> ExcludedFolders { get; init; } = [];

    [ProtoMember(4)]
    public required List<string> ExcludedFiles { get; init; } = [];
}

[ProtoContract]
public class CountThumbnailableFilesResponseDto
{
    [ProtoMember(1)]
    public required int FileCount { get; init; }

    [ProtoMember(2)]
    public required long TotalSizeInBytes { get; init; }
}
