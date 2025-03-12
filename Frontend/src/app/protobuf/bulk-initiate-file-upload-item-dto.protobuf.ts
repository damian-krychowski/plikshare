import * as protobuf from "protobufjs";

export function getBulkInitiateFileUploadItemDtoProtobuf() {
    return new protobuf.Type("BulkInitiateFileUploadItemDto")
        .add(new protobuf.Field("fileUploadExternalId", 1, "string"))
        .add(new protobuf.Field("folderExternalId", 2, "string"))
        .add(new protobuf.Field("fileNameWithExtension", 3, "string"))
        .add(new protobuf.Field("fileContentType", 4, "string"))
        .add(new protobuf.Field("fileSizeInBytes", 5, "int64"));
}