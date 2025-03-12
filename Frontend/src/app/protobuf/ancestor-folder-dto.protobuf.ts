import * as protobuf from "protobufjs";

export function getAncestorFolderDtoProtobuf() {
    return new protobuf.Type("AncestorFolderDto")
        .add(new protobuf.Field("externalId", 1, "string"))
        .add(new protobuf.Field("name", 2, "string"));
}