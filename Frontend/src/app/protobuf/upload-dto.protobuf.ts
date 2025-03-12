import * as protobuf from "protobufjs";

export function getUploadDtoProtobuf() {
    return new protobuf.Type("UploadDto")
        .add(new protobuf.Field("externalId", 1, "string"))
        .add(new protobuf.Field("fileName", 2, "string"))
        .add(new protobuf.Field("fileExtension", 3, "string"))
        .add(new protobuf.Field("fileContentType", 4, "string"))
        .add(new protobuf.Field("fileSizeInBytes", 5, "int64"))
        .add(new protobuf.Field("alreadyUploadedPartNumbers", 6, "int32", "repeated"));
}
