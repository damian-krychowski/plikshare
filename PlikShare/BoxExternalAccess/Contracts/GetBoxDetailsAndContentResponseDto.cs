using PlikShare.Folders.List.Contracts;
using ProtoBuf;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

namespace PlikShare.BoxExternalAccess.Contracts;

[ProtoContract]
public class GetBoxDetailsAndContentResponseDto
{
    [ProtoMember(1)]
    public required BoxDetailsDto Details { get; init; }

    [ProtoMember(2)]
    public required CurrentFolderDto? Folder { get; init; }

    [ProtoMember(3)]
    public required List<SubfolderDto> Subfolders { get; init; }

    [ProtoMember(4)]
    public required List<FileDto> Files { get; init; }

    [ProtoMember(5)]
    public required List<UploadDto> Uploads { get; init; }
}

[ProtoContract]
public class BoxDetailsDto
{
    [ProtoMember(1)]
    public required bool IsTurnedOn { get; init; }

    [ProtoMember(2)]
    public required string? Name { get; init; }

    [ProtoMember(3)]
    public required string? OwnerEmail { get; init; }

    [ProtoMember(4)]
    public required string? WorkspaceExternalId { get; init; }

    [ProtoMember(5)]
    public required bool AllowDownload { get; init; }

    [ProtoMember(6)]
    public required bool AllowUpload { get; init; }

    [ProtoMember(7)]
    public required bool AllowList { get; init; }

    [ProtoMember(8)]
    public required bool AllowDeleteFile { get; init; }

    [ProtoMember(9)]
    public required bool AllowRenameFile { get; init; }

    [ProtoMember(10)]
    public required bool AllowMoveItems { get; init; }

    [ProtoMember(11)]
    public required bool AllowCreateFolder { get; init; }

    [ProtoMember(12)]
    public required bool AllowRenameFolder { get; init; }

    [ProtoMember(13)]
    public required bool AllowDeleteFolder { get; init; }
}