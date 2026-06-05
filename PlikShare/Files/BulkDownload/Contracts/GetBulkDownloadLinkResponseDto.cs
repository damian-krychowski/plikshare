using ProtoBuf;

namespace PlikShare.Files.BulkDownload.Contracts;

[ProtoContract]
public class GetBulkDownloadLinkRequestDto
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
public class GetBulkDownloadLinkResponseDto
{
    [ProtoMember(1)]
    public required string PreSignedUrl { get; init; }
}
