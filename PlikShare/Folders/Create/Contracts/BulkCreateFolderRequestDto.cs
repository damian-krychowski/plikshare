using ProtoBuf;

namespace PlikShare.Folders.Create.Contracts;

[ProtoContract]
public class BulkCreateFolderRequestDto
{
    [ProtoMember(1)]
    public required string? ParentExternalId { get; init; }

    [ProtoMember(2)]
    public required bool EnsureUniqueNames { get; init; }

    [ProtoMember(3)]
    public required List<FolderTreeDto> FolderTrees { get; init; }
}

[ProtoContract]
public class FolderTreeDto
{
    [ProtoMember(1)]
    public required int TemporaryId { get; init; }

    [ProtoMember(2)]
    public required string Name { get; init; }

    [ProtoMember(3)]
    public required List<FolderTreeDto>? Subfolders { get; init; }
}