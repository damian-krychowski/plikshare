import * as protobuf from "protobufjs";
import { getCurrentFolderDtoProtobuf } from "./current-folder-dto.protobuf";
import { getSubfolderDtoProtobuf } from "./subfolder-dto.protobuf";
import { getFileDtoProtobuf } from "./file-dto.protobuf";
import { getUploadDtoProtobuf } from "./upload-dto.protobuf";
import { getBoxDetailsDtoProtobuf } from "./box-details-dto.protobuf";

export function getBoxDetailsAndContentResponseDtoProtobuf() {
    return new protobuf.Type("GetBoxDetailsAndContentResponseDto")
        .add(getBoxDetailsDtoProtobuf())
        .add(getCurrentFolderDtoProtobuf())
        .add(getSubfolderDtoProtobuf())
        .add(getFileDtoProtobuf())
        .add(getUploadDtoProtobuf())
        .add(new protobuf.Field("details", 1, "BoxDetailsDto"))
        .add(new protobuf.Field("folder", 2, "CurrentFolderDto"))
        .add(new protobuf.Field("subfolders", 3, "SubfolderDto", "repeated"))
        .add(new protobuf.Field("files", 4, "FileDto", "repeated"))
        .add(new protobuf.Field("uploads", 5, "UploadDto", "repeated"));
}