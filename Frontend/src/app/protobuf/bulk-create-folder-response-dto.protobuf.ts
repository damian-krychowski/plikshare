import * as protobuf from "protobufjs";
import { getBulkCreateFolderItemDtoProtobuf } from "./bulk-create-folder-item-dto.protobuf";

export function getBulkCreateFolderResponseDtoProtobuf() {
    return new protobuf.Type("BulkCreateFolderResponseDto")
        .add(getBulkCreateFolderItemDtoProtobuf())
        .add(new protobuf.Field("items", 1, "BulkCreateFolderItemDto", "repeated"));
}