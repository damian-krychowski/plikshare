using ProtoBuf;

namespace PlikShare.Files.Preview.GetZipBulkDownloadLink.Contracts;

[ProtoContract]
public class GetZipBulkDownloadLinkRequestDto
{
    [ProtoMember(1)]
    public required uint[] SelectedFolderIds { get; init; } = [];

    [ProtoMember(2)]
    public required uint[] SelectedEntryIndices { get; init; } = [];

    [ProtoMember(3)]
    public required uint[] ExcludedFolderIds { get; init; } = [];

    [ProtoMember(4)]
    public required uint[] ExcludedEntryIndices { get; init; } = [];
}

[ProtoContract]
public class GetZipBulkDownloadLinkResponseDto
{
    [ProtoMember(1)]
    public required string DownloadPreSignedUrl { get; init; }
}
