import * as protobuf from "protobufjs";

export function getZipFileDetailsItemDtoProtobuf() {
    return new protobuf.Type("GetZipFileDetailsItemDto")
        .add(new protobuf.Field("fileName", 1, "string"))
        .add(new protobuf.Field("virtualFolderId", 2, "uint32"))
        .add(new protobuf.Field("compressedSizeInBytes", 3, "int64"))
        .add(new protobuf.Field("sizeInBytes", 4, "int64"))
        .add(new protobuf.Field("offsetToLocalFileHeader", 5, "int64"))
        .add(new protobuf.Field("fileNameLength", 6, "uint32"))
        .add(new protobuf.Field("compressionMethod", 7, "uint32"))
        .add(new protobuf.Field("indexInArchive", 8, "uint32"));
}
