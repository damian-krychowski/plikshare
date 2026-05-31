using ProtoBuf;

namespace PlikShare.MediaProcessing.Generation.Contracts;

[ProtoContract]
public class GenerateFileThumbnailsBulkRequestDto
{
    [ProtoMember(1)]
    public required List<string> FileExternalIds { get; init; }

    // Variant names ("Mini" / "Small" / "Large") rather than ints — `repeated string` is length-
    // delimited and unambiguous across protobuf libraries, sidestepping the packed/unpacked
    // discrepancy that bites `repeated int32` between protobufjs and protobuf-net.
    [ProtoMember(2)]
    public required List<string> Variants { get; init; }
}
