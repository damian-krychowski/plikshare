import * as protobuf from "protobufjs";

// Mirrors PlikShare.MediaProcessing.Generation.Contracts.GenerateFileThumbnailsBulkRequestDto.
// Include/exclude tree selection (same shape as bulk-download) — the server resolves it into the
// flat list of files to process. `variants` is repeated string ("Mini" / "Small" / "Large").
export function getGenerateFileThumbnailsBulkRequestDtoProtobuf() {
    return new protobuf.Type("GenerateFileThumbnailsBulkRequestDto")
        .add(new protobuf.Field("selectedFolders", 1, "string", "repeated"))
        .add(new protobuf.Field("selectedFiles", 2, "string", "repeated"))
        .add(new protobuf.Field("excludedFolders", 3, "string", "repeated"))
        .add(new protobuf.Field("excludedFiles", 4, "string", "repeated"))
        .add(new protobuf.Field("variants", 5, "string", "repeated"));
}
