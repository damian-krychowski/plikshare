import * as protobuf from "protobufjs";

// Mirrors PlikShare.QuickShares.Create.Contracts.CreateQuickShareResponseDto.
export function getCreateQuickShareResponseDtoProtobuf() {
    return new protobuf.Type("CreateQuickShareResponseDto")
        .add(new protobuf.Field("externalId", 1, "string"))
        .add(new protobuf.Field("slug", 2, "string"))
        .add(new protobuf.Field("url", 3, "string"));
}
