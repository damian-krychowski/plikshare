using PlikShare.Folders.Id;
using ProtoBuf;

namespace PlikShare.Workspaces.SearchFilesTree.Contracts;

public class SearchFilesTreeRequestDto
{
    public required string Phrase { get; init; }
    public required FolderExtId? FolderExternalId { get; init; }
}

[ProtoContract]
public class SearchFilesTreeResponseDto
{
    [ProtoMember(1)]
    public required List<string> FolderExternalIds { get; init; }

    [ProtoMember(2)]
    public required List<SearchFilesTreeFolderItemDto> Folders { get; init; }

    [ProtoMember(3)]
    public required List<SearchFilesTreeFileItemDto> Files { get; init; }
    
    //when this field is there it means the query returned too many results, and they were not sent back to the browser
    //for now 'too many' means more than 1000

    [ProtoMember(4)]
    public required int TooManyResultsCounter { get; init; }
}

[ProtoContract]
public class SearchFilesTreeFolderItemDto
{
    [ProtoMember(1)]
    public required string Name { get; init; }

    [ProtoMember(2)]
    public int IdIndex { get; init; }

    [ProtoMember(3)]
    public int ParentIdIndex { get; init; } //-1 if does not have a parent (to simplify on the js side when null in proto is translated to 0)

    [ProtoMember(4)]
    public required bool WasCreatedByUser { get; init; }

    [ProtoMember(5)]
    public required DateTime? CreatedAt { get; init; }
}

[ProtoContract]
public class SearchFilesTreeFileItemDto
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
    
    [ProtoMember(7)]
    public required int FolderIdIndex { get; init; } //-1 if does not have a parent (to simplify on the js side when null in proto is translated to 0)
}