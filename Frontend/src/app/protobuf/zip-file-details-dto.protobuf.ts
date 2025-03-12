import * as protobuf from "protobufjs";
import { getZipFileDetailsItemDtoProtobuf } from "./zip-file-details-item-dto.protobuf";

export function getZipFileDetailsDtoProtobuf() {
    return new protobuf.Type("GetZipFileDetailsResponseDto")
        .add(getZipFileDetailsItemDtoProtobuf())
        .add(new protobuf.Field("items", 1, "GetZipFileDetailsItemDto", "repeated"));
}