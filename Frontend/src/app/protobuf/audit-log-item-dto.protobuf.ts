import * as protobuf from "protobufjs";

export function getAuditLogItemDtoProtobuf() {
    return new protobuf.Type("AuditLogItemDto")
        .add(new protobuf.Field("externalId", 1, "string"))
        .add(new protobuf.Field("createdAt", 2, "string"))
        .add(new protobuf.Field("actorEmail", 3, "string"))
        .add(new protobuf.Field("actorIdentity", 4, "string"))
        .add(new protobuf.Field("eventType", 5, "string"))
        .add(new protobuf.Field("eventSeverity", 6, "string"));
}
