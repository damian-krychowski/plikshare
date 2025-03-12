using PlikShare.Folders.Id;
using ProtoBuf;

namespace PlikShare.Folders.Create.Contracts;

public class CreateFolderResponseDto
{
    public required FolderExtId ExternalId { get; init; }
}


[ProtoContract]
public class BulkCreateFolderResponseDto
{
    [ProtoMember(1)]
    public required List<BulkCreateFolderItemDto> Items { get; init; }
}

[ProtoContract]
public class BulkCreateFolderItemDto
{
    [ProtoMember(1)]
    public required int TemporaryId { get; init; }

    [ProtoMember(2)]
    public required string ExternalId { get; init; }
}