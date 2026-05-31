import * as protobuf from "protobufjs";

// Mirrors PlikShare.MediaProcessing.Generation.Contracts.GenerateFileThumbnailsBulkResponseDto.
// batchId travels as string (Guid on the server, no native protobuf type for it).
export function getGenerateFileThumbnailsBulkResponseDtoProtobuf() {
    return new protobuf.Type("GenerateFileThumbnailsBulkResponseDto")
        .add(new protobuf.Field("batchId", 1, "string"))
        .add(new protobuf.Field("totalFiles", 2, "int32"));
}
