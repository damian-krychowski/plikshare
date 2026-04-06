import * as protobuf from "protobufjs";
import { getAuditLogItemDtoProtobuf } from "./audit-log-item-dto.protobuf";

export function getAuditLogResponseDtoProtobuf() {
    return new protobuf.Type("GetAuditLogResponseDto")
        .add(getAuditLogItemDtoProtobuf())
        .add(new protobuf.Field("items", 1, "AuditLogItemDto", "repeated"))
        .add(new protobuf.Field("nextCursor", 2, "int32"))
        .add(new protobuf.Field("hasMore", 3, "bool"));
}
