using ProtoBuf;

namespace PlikShare.Uploads.Initiate.Contracts;

[ProtoContract]
public class BulkInitiateFileUploadRequestDto
{
    [ProtoMember(1)]
    public required BulkInitiateFileUploadItemDto[] Items { get; init; }
}

[ProtoContract]
public class BulkInitiateFileUploadItemDto
{
    [ProtoMember(1)]
    public required string FileUploadExternalId { get; init; }

    [ProtoMember(2)]
    public required string? FolderExternalId { get; set; } //that is set on purpose - as box scenario is setting this for box top folder

    [ProtoMember(3)]
    public required string FileNameWithExtension { get; init; }

    [ProtoMember(4)]
    public required string FileContentType { get; init; }

    [ProtoMember(5)]
    public required long FileSizeInBytes { get; init; }
}

[ProtoContract]
public class BulkInitiateFileUploadResponseDto
{
    [ProtoMember(1)]
    public required BulkInitiateDirectUploadsResponseDto? DirectUploads { get; init; }

    [ProtoMember(2)]
    public required List<BulkInitiateSingleChunkUploadResponseDto> SingleChunkUploads { get; init; }

    [ProtoMember(3)]
    public required List<BulkInitiateMultiStepChunkUploadResponseDto> MultiStepChunkUploads { get; init; }

    [ProtoMember(4)]
    public required long? NewWorkspaceSizeInBytes { get; init; }
}

[ProtoContract]
public class BulkInitiateDirectUploadsResponseDto
{
    [ProtoMember(1)]
    public required int Count { get; init; }

    [ProtoMember(2)]
    public required string PreSignedMultiFileDirectUploadLink { get; init; }
}

[ProtoContract]
public class BulkInitiateSingleChunkUploadResponseDto
{
    [ProtoMember(1)]
    public required string FileUploadExternalId { get; init; }

    [ProtoMember(2)]
    public required string PreSignedUploadLink { get; init; }
}

[ProtoContract]
public class BulkInitiateMultiStepChunkUploadResponseDto
{
    [ProtoMember(1)]
    public required string FileUploadExternalId { get; init; }

    [ProtoMember(2)]
    public required int ExpectedPartsCount { get; init; }
}