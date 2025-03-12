import * as protobuf from "protobufjs";

export function getBulkCreateFolderItemDtoProtobuf() {
    return new protobuf.Type("BulkCreateFolderItemDto")
        .add(new protobuf.Field("temporaryId", 1, "int32"))
        .add(new protobuf.Field("externalId", 2, "string"));
}