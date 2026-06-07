import * as protobuf from "protobufjs";
import { getDateTimeProtobuf } from "./datetime.protobuf";
import { getFileMetadataProtobuf } from "./file-metadata.protobuf";

export function getFileDtoProtobuf() {
    return new protobuf.Type("FileDto")
        .add(new protobuf.Field("externalId", 1, "string"))
        .add(new protobuf.Field("name", 2, "string"))
        .add(new protobuf.Field("extension", 3, "string"))
        .add(new protobuf.Field("sizeInBytes", 4, "int64"))
        .add(new protobuf.Field("isLocked", 5, "bool"))
        .add(new protobuf.Field("wasUploadedByUser", 6, "bool"))
        .add(new protobuf.Field("createdAt", 7, "appDateTime"))
        .add(new protobuf.Field("position", 8, "int64"))
        .add(new protobuf.Field("metadata", 9, "FileMetadataDto", "optional"))
        .add(getFileMetadataProtobuf())
        .add(getDateTimeProtobuf());
}