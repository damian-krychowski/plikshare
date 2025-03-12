import * as protobuf from "protobufjs";
import { getBulkInitiateFileUploadItemDtoProtobuf } from "./bulk-initiate-file-upload-item-dto.protobuf";

export function getBulkInitiateFileUploadRequestDtoProtobuf() {
    return new protobuf.Type("BulkInitiateFileUploadRequestDto")
        .add(getBulkInitiateFileUploadItemDtoProtobuf())
        .add(new protobuf.Field("items", 1, "BulkInitiateFileUploadItemDto", "repeated"));
}