import * as protobuf from "protobufjs";

// Mirrors PlikShare.MediaProcessing.Generation.Contracts.GenerateFileThumbnailsBulkRequestDto.
// `variants` is repeated string ("Mini" / "Small" / "Large") rather than repeated int — length-
// delimited wire format is unambiguous across protobufjs and protobuf-net (no packed/unpacked
// discrepancy that affects repeated scalars).
export function getGenerateFileThumbnailsBulkRequestDtoProtobuf() {
    return new protobuf.Type("GenerateFileThumbnailsBulkRequestDto")
        .add(new protobuf.Field("fileExternalIds", 1, "string", "repeated"))
        .add(new protobuf.Field("variants", 2, "string", "repeated"));
}
