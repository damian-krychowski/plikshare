using ProtoBuf;

namespace PlikShare.MediaProcessing.Generation.Contracts;

[ProtoContract]
public class GenerateFileThumbnailsBulkResponseDto
{
    // Guid app-side, string on the wire — protobuf has no native Guid and the rest of the codebase
    // serializes Guid ids as strings.
    [ProtoMember(1)]
    public required string BatchId { get; init; }

    [ProtoMember(2)]
    public required int TotalFiles { get; init; }
}
