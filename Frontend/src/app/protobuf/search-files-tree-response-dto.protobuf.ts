import * as protobuf from "protobufjs";
import { getDateTimeProtobuf } from "./datetime.protobuf";

export function getSearchFilesTreeResponseDtoProtobuf() {
    const folder = new protobuf.Type("SearchFilesTreeFolderItemDto")
        .add(new protobuf.Field("name", 1, "string"))
        .add(new protobuf.Field("idIndex", 2, "int32"))
        .add(new protobuf.Field("parentIdIndex", 3, "int32"))
        .add(new protobuf.Field("wasCreatedByUser", 4, "bool"))
        .add(new protobuf.Field("createdAt", 5, "appDateTime"))
        .add(getDateTimeProtobuf());

    const file = new protobuf.Type("SearchFilesTreeFileItemDto")
        .add(new protobuf.Field("externalId", 1, "string"))
        .add(new protobuf.Field("name", 2, "string"))
        .add(new protobuf.Field("extension", 3, "string"))
        .add(new protobuf.Field("sizeInBytes", 4, "int64"))
        .add(new protobuf.Field("isLocked", 5, "bool"))
        .add(new protobuf.Field("wasUploadedByUser", 6, "bool"))
        .add(new protobuf.Field("folderIdIndex", 7, "int32"));

    return new protobuf.Type("SearchFilesTreeResponseDto")
        .add(folder)
        .add(file)
        .add(new protobuf.Field("folderExternalIds", 1, "string", "repeated"))
        .add(new protobuf.Field("folders", 2, "SearchFilesTreeFolderItemDto", "repeated"))
        .add(new protobuf.Field("files", 3, "SearchFilesTreeFileItemDto", "repeated"))
        .add(new protobuf.Field("tooManyResultsCounter", 4, "int32"));
}