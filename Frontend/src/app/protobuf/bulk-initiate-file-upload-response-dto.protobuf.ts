import * as protobuf from "protobufjs";

export function getBulkInitiateFileUploadResponseDtoProtobuf() {
    const directUploads = new protobuf.Type("BulkInitiateDirectUploadsResponseDto")
        .add(new protobuf.Field("count", 1, "int32"))
        .add(new protobuf.Field("preSignedMultiFileDirectUploadLink", 2, "string"));

    const singleChunkUploads = new protobuf.Type("BulkInitiateSingleChunkUploadResponseDto")
        .add(new protobuf.Field("fileUploadExternalId", 1, "string"))
        .add(new protobuf.Field("preSignedUploadLink", 2, "string"));

    const multiStepChunkUploads = new protobuf.Type("BulkInitiateMultiStepChunkUploadResponseDto")
        .add(new protobuf.Field("fileUploadExternalId", 1, "string"))
        .add(new protobuf.Field("expectedPartsCount", 2, "int32"));

    return new protobuf.Type("BulkInitiateFileUploadResponseDto")
        .add(directUploads)
        .add(singleChunkUploads)
        .add(multiStepChunkUploads)
        .add(new protobuf.Field("directUploads", 1, "BulkInitiateDirectUploadsResponseDto"))
        .add(new protobuf.Field("singleChunkUploads", 2, "BulkInitiateSingleChunkUploadResponseDto", "repeated"))
        .add(new protobuf.Field("multiStepChunkUploads", 3, "BulkInitiateMultiStepChunkUploadResponseDto", "repeated"));
}