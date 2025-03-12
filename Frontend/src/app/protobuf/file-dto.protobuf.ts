import * as protobuf from "protobufjs";

export function getFileDtoProtobuf() {
    return new protobuf.Type("FileDto")
        .add(new protobuf.Field("externalId", 1, "string"))
        .add(new protobuf.Field("name", 2, "string"))
        .add(new protobuf.Field("extension", 3, "string"))
        .add(new protobuf.Field("sizeInBytes", 4, "int64"))
        .add(new protobuf.Field("isLocked", 5, "bool"))
        .add(new protobuf.Field("wasUploadedByUser", 6, "bool"));
}