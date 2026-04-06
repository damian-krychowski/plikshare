import * as protobuf from "protobufjs";

export function getAuditLogItemDtoProtobuf() {
    return new protobuf.Type("AuditLogItemDto")
        .add(new protobuf.Field("externalId", 1, "string"))
        .add(new protobuf.Field("createdAt", 2, "string"))
        .add(new protobuf.Field("correlationId", 3, "string"))
        .add(new protobuf.Field("actorIdentityType", 4, "string"))
        .add(new protobuf.Field("actorIdentity", 5, "string"))
        .add(new protobuf.Field("actorEmail", 6, "string"))
        .add(new protobuf.Field("actorIp", 7, "string"))
        .add(new protobuf.Field("eventCategory", 8, "string"))
        .add(new protobuf.Field("eventType", 9, "string"))
        .add(new protobuf.Field("eventSeverity", 10, "string"))
        .add(new protobuf.Field("workspaceExternalId", 11, "string"))
        .add(new protobuf.Field("details", 12, "string"));
}
