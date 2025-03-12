import * as protobuf from "protobufjs";

export function getFolderTreeDtoProtobuf() {
    return new protobuf.Type("FolderTreeDto")
        .add(new protobuf.Field("temporaryId", 1, "int32"))
        .add(new protobuf.Field("name", 2, "string"))
        .add(new protobuf.Field("subfolders", 3, "FolderTreeDto", "repeated"));
}