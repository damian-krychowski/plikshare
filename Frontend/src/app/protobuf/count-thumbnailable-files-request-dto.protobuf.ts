import * as protobuf from "protobufjs";

// Mirrors PlikShare.MediaProcessing.Generation.Contracts.CountThumbnailableFilesRequestDto.
// Same include/exclude tree selection shape as the generate-bulk request — the server resolves it
// and returns how many files (and their total size) would be processed.
export function getCountThumbnailableFilesRequestDtoProtobuf() {
    return new protobuf.Type("CountThumbnailableFilesRequestDto")
        .add(new protobuf.Field("selectedFolders", 1, "string", "repeated"))
        .add(new protobuf.Field("selectedFiles", 2, "string", "repeated"))
        .add(new protobuf.Field("excludedFolders", 3, "string", "repeated"))
        .add(new protobuf.Field("excludedFiles", 4, "string", "repeated"));
}

// Mirrors CountThumbnailableFilesResponseDto. totalSizeInBytes is int64 (a byte total can exceed
// int32), so it decodes to a protobufjs Long — the caller wraps it in Number(...).
export function getCountThumbnailableFilesResponseDtoProtobuf() {
    return new protobuf.Type("CountThumbnailableFilesResponseDto")
        .add(new protobuf.Field("fileCount", 1, "int32"))
        .add(new protobuf.Field("totalSizeInBytes", 2, "int64"));
}
