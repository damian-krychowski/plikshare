using ProtoBuf;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

namespace PlikShare.Folders.List.Contracts;

[ProtoContract]
public class GetTopFolderContentResponseDto
{
    [ProtoMember(1)]
    public required List<SubfolderDto> Subfolders { get; init; }

    [ProtoMember(2)]
    public required List<FileDto> Files { get; init; }

    [ProtoMember(3)]
    public required List<UploadDto> Uploads { get; init; }
}

[ProtoContract]
public class SubfolderDto
{
    [ProtoMember(1)]
    public required string ExternalId { get; init; }

    [ProtoMember(2)]
    public required string Name { get; init; }

    [ProtoMember(3)]
    public required bool WasCreatedByUser { get; init; }

    [ProtoMember(4)]
    public required DateTime? CreatedAt { get; init; }
}

[ProtoContract]
public class FileDto
{
    [ProtoMember(1)]
    public required string ExternalId { get; init; }

    [ProtoMember(2)]
    public required string Name { get; init; }

    [ProtoMember(3)]
    public required string Extension { get; init; }

    [ProtoMember(4)]
    public required long SizeInBytes { get; init; }

    [ProtoMember(5)]
    public required bool IsLocked { get; init; }

    [ProtoMember(6)]
    public required bool WasUploadedByUser { get; init; }
}

[ProtoContract]
public class UploadDto
{
    [ProtoMember(1)]
    public required string ExternalId { get; init; }

    [ProtoMember(2)]
    public required string FileName { get; init; }

    [ProtoMember(3)]
    public required string FileExtension { get; init; }

    [ProtoMember(4)]
    public required string FileContentType { get; init; }

    [ProtoMember(5)]
    public required long FileSizeInBytes { get; init; }

    [ProtoMember(6)]
    public required List<int> AlreadyUploadedPartNumbers { get; init; }
}