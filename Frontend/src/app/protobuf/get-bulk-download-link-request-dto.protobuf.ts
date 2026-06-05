import * as protobuf from "protobufjs";

// Mirrors PlikShare.Files.BulkDownload.Contracts.GetBulkDownloadLinkRequestDto.
// External ids travel as plain strings (no ExtId wrappers); field numbers match the
// [ProtoMember] order on the C# side.
export function getBulkDownloadLinkRequestDtoProtobuf() {
    return new protobuf.Type("GetBulkDownloadLinkRequestDto")
        .add(new protobuf.Field("selectedFolders", 1, "string", "repeated"))
        .add(new protobuf.Field("selectedFiles", 2, "string", "repeated"))
        .add(new protobuf.Field("excludedFolders", 3, "string", "repeated"))
        .add(new protobuf.Field("excludedFiles", 4, "string", "repeated"));
}
