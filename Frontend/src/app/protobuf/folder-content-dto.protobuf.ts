import * as protobuf from "protobufjs";
import { getCurrentFolderDtoProtobuf } from "./current-folder-dto.protobuf";
import { getSubfolderDtoProtobuf } from "./subfolder-dto.protobuf";
import { getFileDtoProtobuf } from "./file-dto.protobuf";
import { getUploadDtoProtobuf } from "./upload-dto.protobuf";

export function getFolderContentDtoProtobuf() {
    return new protobuf.Type("GetFolderContentResponseDto")
        .add(getCurrentFolderDtoProtobuf())
        .add(getSubfolderDtoProtobuf())
        .add(getFileDtoProtobuf())
        .add(getUploadDtoProtobuf())
        .add(new protobuf.Field("folder", 1, "CurrentFolderDto"))
        .add(new protobuf.Field("subfolders", 2, "SubfolderDto", "repeated"))
        .add(new protobuf.Field("files", 3, "FileDto", "repeated"))
        .add(new protobuf.Field("uploads", 4, "UploadDto", "repeated"));
}