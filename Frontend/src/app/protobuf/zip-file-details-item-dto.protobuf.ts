import * as protobuf from "protobufjs";

export function getZipFileDetailsItemDtoProtobuf() {
    return new protobuf.Type("GetZipFileDetailsItemDto")
        .add(new protobuf.Field("filePath", 1, "string"))
        .add(new protobuf.Field("compressedSizeInBytes", 2, "int64"))
        .add(new protobuf.Field("sizeInBytes", 3, "int64"))
        .add(new protobuf.Field("offsetToLocalFileHeader", 4, "int64"))
        .add(new protobuf.Field("fileNameLength", 5, "uint32"))
        .add(new protobuf.Field("compressionMethod", 6, "uint32"))
        .add(new protobuf.Field("indexInArchive", 7, "uint32"));
}