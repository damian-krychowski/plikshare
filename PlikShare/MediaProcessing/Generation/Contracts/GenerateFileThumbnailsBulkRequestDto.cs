using ProtoBuf;

namespace PlikShare.MediaProcessing.Generation.Contracts;

[ProtoContract]
public class GenerateFileThumbnailsBulkRequestDto
{
    [ProtoMember(1)]
    public required List<string> SelectedFolders { get; init; } = [];

    [ProtoMember(2)]
    public required List<string> SelectedFiles { get; init; } = [];

    [ProtoMember(3)]
    public required List<string> ExcludedFolders { get; init; } = [];

    [ProtoMember(4)]
    public required List<string> ExcludedFiles { get; init; } = [];

    // Variant names ("Mini" / "Small" / "Large") rather than ints — `repeated string` is length-
    // delimited and unambiguous across protobuf libraries, sidestepping the packed/unpacked
    // discrepancy that bites `repeated int32` between protobufjs and protobuf-net.
    [ProtoMember(5)]
    public required List<string> Variants { get; init; } = [];
}
