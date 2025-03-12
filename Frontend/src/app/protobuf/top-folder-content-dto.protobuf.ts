import * as protobuf from "protobufjs";
import { getFileDtoProtobuf } from "./file-dto.protobuf";
import { getSubfolderDtoProtobuf } from "./subfolder-dto.protobuf";
import { getUploadDtoProtobuf } from "./upload-dto.protobuf";

export function getTopFolderContetDtoProtobuf() {
    return new protobuf.Type("GetTopFoldersResponseDto")
        .add(getSubfolderDtoProtobuf())
        .add(getFileDtoProtobuf())
        .add(getUploadDtoProtobuf())
        .add(new protobuf.Field("subfolders", 1, "SubfolderDto", "repeated"))
        .add(new protobuf.Field("files", 2, "FileDto", "repeated"))
        .add(new protobuf.Field("uploads", 3, "UploadDto", "repeated"));
}