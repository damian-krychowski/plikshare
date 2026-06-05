import * as protobuf from "protobufjs";

// Mirrors PlikShare.Files.Preview.GetZipBulkDownloadLink.Contracts.GetZipBulkDownloadLinkRequestDto.
// Folder ids and entry indices travel as repeated uint32; field numbers match the
// [ProtoMember] order on the C# side.
export function getZipBulkDownloadLinkRequestDtoProtobuf() {
    return new protobuf.Type("GetZipBulkDownloadLinkRequestDto")
        .add(new protobuf.Field("selectedFolderIds", 1, "uint32", "repeated"))
        .add(new protobuf.Field("selectedEntryIndices", 2, "uint32", "repeated"))
        .add(new protobuf.Field("excludedFolderIds", 3, "uint32", "repeated"))
        .add(new protobuf.Field("excludedEntryIndices", 4, "uint32", "repeated"));
}
