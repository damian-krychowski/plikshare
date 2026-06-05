import * as protobuf from "protobufjs";

// Mirrors PlikShare.QuickShares.Create.Contracts.CreateQuickShareRequestDto.
// External ids travel as plain strings; mode is the kebab enum name ("browser" /
// "direct"); expiresAt is an ISO-8601 string. Nullable fields (customSlug,
// expiresAt, password, maxDownloads) are simply omitted on the wire when null.
export function getCreateQuickShareRequestDtoProtobuf() {
    return new protobuf.Type("CreateQuickShareRequestDto")
        .add(new protobuf.Field("name", 1, "string"))
        .add(new protobuf.Field("customSlug", 2, "string"))
        .add(new protobuf.Field("selectedFiles", 3, "string", "repeated"))
        .add(new protobuf.Field("selectedFolders", 4, "string", "repeated"))
        .add(new protobuf.Field("excludedFiles", 5, "string", "repeated"))
        .add(new protobuf.Field("excludedFolders", 6, "string", "repeated"))
        .add(new protobuf.Field("mode", 7, "string"))
        .add(new protobuf.Field("allowIndividualFileDownload", 8, "bool"))
        .add(new protobuf.Field("expiresAt", 9, "string"))
        .add(new protobuf.Field("password", 10, "string"))
        .add(new protobuf.Field("maxDownloads", 11, "int32"));
}
