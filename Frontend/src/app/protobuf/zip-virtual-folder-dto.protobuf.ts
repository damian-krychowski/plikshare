import * as protobuf from "protobufjs";

export function getZipVirtualFolderDtoProtobuf() {
    return new protobuf.Type("ZipVirtualFolderDto")
        .add(new protobuf.Field("id", 1, "uint32"))
        .add(new protobuf.Field("parentId", 2, "uint32"))
        .add(new protobuf.Field("name", 3, "string"));
}
