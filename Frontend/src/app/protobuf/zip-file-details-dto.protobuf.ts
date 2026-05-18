import * as protobuf from "protobufjs";
import { getZipFileDetailsItemDtoProtobuf } from "./zip-file-details-item-dto.protobuf";
import { getZipVirtualFolderDtoProtobuf } from "./zip-virtual-folder-dto.protobuf";

export function getZipFileDetailsDtoProtobuf() {
    return new protobuf.Type("GetZipFileDetailsResponseDto")
        .add(getZipFileDetailsItemDtoProtobuf())
        .add(getZipVirtualFolderDtoProtobuf())
        .add(new protobuf.Field("items", 1, "GetZipFileDetailsItemDto", "repeated"))
        .add(new protobuf.Field("folders", 2, "ZipVirtualFolderDto", "repeated"));
}
