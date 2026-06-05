import * as protobuf from "protobufjs";

// Mirrors PlikShare.Files.BulkDownload.Contracts.GetBulkDownloadLinkResponseDto.
export function getBulkDownloadLinkResponseDtoProtobuf() {
    return new protobuf.Type("GetBulkDownloadLinkResponseDto")
        .add(new protobuf.Field("preSignedUrl", 1, "string"));
}
