import * as protobuf from "protobufjs";

export function getFileMetadataProtobuf() {
    const thumbnail = new protobuf.Type("ThumbnailMetadataDto")
        .add(new protobuf.Field("miniEtag", 1, "string"));

    const dimensions = new protobuf.Type("DimensionsMetadataDto")
        .add(new protobuf.Field("width", 1, "int32"))
        .add(new protobuf.Field("height", 2, "int32"));

    return new protobuf.Type("FileMetadataDto")
        .add(thumbnail)
        .add(dimensions)
        .add(new protobuf.Field("thumbnail", 1, "ThumbnailMetadataDto", "optional"))
        .add(new protobuf.Field("dimensions", 2, "DimensionsMetadataDto", "optional"));
}
