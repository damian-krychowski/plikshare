import * as protobuf from "protobufjs";
import { getAncestorFolderDtoProtobuf } from "./ancestor-folder-dto.protobuf";

export function getCurrentFolderDtoProtobuf() {
    return new protobuf.Type("CurrentFolderDto")        
        .add(getAncestorFolderDtoProtobuf())
        .add(new protobuf.Field("name", 1, "string"))
        .add(new protobuf.Field("externalId", 2, "string"))
        .add(new protobuf.Field("ancestors", 3, "AncestorFolderDto", "repeated"));
}