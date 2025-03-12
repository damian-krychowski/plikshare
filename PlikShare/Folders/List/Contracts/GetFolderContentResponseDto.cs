using ProtoBuf;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

namespace PlikShare.Folders.List.Contracts;

[ProtoContract]
public class GetFolderContentResponseDto
{
    [ProtoMember(1)]
    public required CurrentFolderDto? Folder { get; init; }

    [ProtoMember(2)]
    public required List<SubfolderDto> Subfolders { get; init; }

    [ProtoMember(3)]
    public required List<FileDto> Files { get; init; }

    [ProtoMember(4)]
    public required List<UploadDto> Uploads { get; init; }

}

[ProtoContract]
public class CurrentFolderDto
{
    [ProtoMember(1)]
    public required string Name { get; init; }

    [ProtoMember(2)]
    public required string ExternalId { get; init; }

    [ProtoMember(3)]
    public required List<AncestorFolderDto> Ancestors { get; init; }
}

[ProtoContract]
public class AncestorFolderDto
{
    [ProtoMember(1)]
    public required string ExternalId { get; init; }

    [ProtoMember(2)]
    public required string Name { get; init; }
}