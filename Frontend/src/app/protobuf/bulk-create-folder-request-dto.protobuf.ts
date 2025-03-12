import * as protobuf from "protobufjs";
import { getFolderTreeDtoProtobuf } from "./folder-tree-dto.protobuf";

export function getBulkCreateFolderRequestDtoProtobuf() {
    return new protobuf.Type("BulkCreateFolderRequestDto")
        .add(new protobuf.Field("parentExternalId", 1, "string"))
        .add(new protobuf.Field("ensureUniqueNames", 2, "bool"))
        .add(getFolderTreeDtoProtobuf())
        .add(new protobuf.Field("folderTrees", 3, "FolderTreeDto", "repeated"));
}