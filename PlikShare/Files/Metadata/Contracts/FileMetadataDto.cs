using ProtoBuf;

namespace PlikShare.Files.Metadata.Contracts;

[ProtoContract]
public class FileMetadataDto
{
    [ProtoMember(1)]
    public ThumbnailMetadataDto? Thumbnail { get; init; }

    [ProtoMember(2)]
    public DimensionsMetadataDto? Dimensions { get; init; }
}

[ProtoContract]
public class ThumbnailMetadataDto
{
    [ProtoMember(1)]
    public string? MiniEtag { get; init; }

    [ProtoMember(2)]
    public string? SmallEtag { get; init; }

    [ProtoMember(3)]
    public string? LargeEtag { get; init; }
}

[ProtoContract]
public class DimensionsMetadataDto
{
    [ProtoMember(1)]
    public required int Width { get; init; }

    [ProtoMember(2)]
    public required int Height { get; init; }
}
