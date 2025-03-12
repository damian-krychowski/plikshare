import * as protobuf from "protobufjs";

export function getBoxDetailsDtoProtobuf() {
    return new protobuf.Type("BoxDetailsDto")
        .add(new protobuf.Field("isTurnedOn", 1, "bool"))
        .add(new protobuf.Field("name", 2, "string"))
        .add(new protobuf.Field("ownerEmail", 3, "string"))
        .add(new protobuf.Field("workspaceExternalId", 4, "string"))
        .add(new protobuf.Field("allowDownload", 5, "bool"))
        .add(new protobuf.Field("allowUpload", 6, "bool"))
        .add(new protobuf.Field("allowList", 7, "bool"))
        .add(new protobuf.Field("allowDeleteFile", 8, "bool"))
        .add(new protobuf.Field("allowRenameFile", 9, "bool"))
        .add(new protobuf.Field("allowMoveItems", 10, "bool"))
        .add(new protobuf.Field("allowCreateFolder", 11, "bool"))
        .add(new protobuf.Field("allowRenameFolder", 12, "bool"))
        .add(new protobuf.Field("allowDeleteFolder", 13, "bool"));
}