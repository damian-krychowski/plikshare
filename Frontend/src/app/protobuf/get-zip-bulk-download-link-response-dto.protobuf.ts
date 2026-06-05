import * as protobuf from "protobufjs";

// Mirrors PlikShare.Files.Preview.GetZipBulkDownloadLink.Contracts.GetZipBulkDownloadLinkResponseDto.
export function getZipBulkDownloadLinkResponseDtoProtobuf() {
    return new protobuf.Type("GetZipBulkDownloadLinkResponseDto")
        .add(new protobuf.Field("downloadPreSignedUrl", 1, "string"));
}
