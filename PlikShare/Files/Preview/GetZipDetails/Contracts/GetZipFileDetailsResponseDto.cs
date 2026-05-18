using ProtoBuf;

namespace PlikShare.Files.Preview.GetZipDetails.Contracts;

[ProtoContract]
public class GetZipFileDetailsResponseDto
{
    [ProtoMember(1)]
    public required List<GetZipFileDetailsItemDto> Items { get; init; }

    [ProtoMember(2)]
    public required List<ZipVirtualFolderDto> Folders { get; init; }
}

[ProtoContract]
public class ZipVirtualFolderDto
{
    [ProtoMember(1)]
    public required uint Id { get; init; }

    [ProtoMember(2)]
    public required uint? ParentId { get; init; }

    [ProtoMember(3)]
    public required string Name { get; init; }
}

[ProtoContract]
public class GetZipFileDetailsItemDto
{
    [ProtoMember(1)]
    public required string FileName { get; init; }

    [ProtoMember(2)]
    public required uint? VirtualFolderId { get; init; }

    [ProtoMember(3)]
    public required long CompressedSizeInBytes { get; init; }

    [ProtoMember(4)]
    public required long SizeInBytes { get; init; }

    [ProtoMember(5)]
    public required long OffsetToLocalFileHeader { get; init; }

    [ProtoMember(6)]
    public required ushort FileNameLength { get; init; }

    [ProtoMember(7)]
    public required ushort CompressionMethod { get; init; }

    [ProtoMember(8)]
    public required uint IndexInArchive { get; init; }
}
