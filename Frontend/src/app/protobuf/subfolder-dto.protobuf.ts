import * as protobuf from "protobufjs";
import { getDateTimeProtobuf } from "./datetime.protobuf";

export function getSubfolderDtoProtobuf() {
    return new protobuf.Type("SubfolderDto")
        .add(new protobuf.Field("externalId", 1, "string"))
        .add(new protobuf.Field("name", 2, "string"))
        .add(new protobuf.Field("wasCreatedByUser", 3, "bool"))
        .add(new protobuf.Field("createdAt", 4, "appDateTime"))
        .add(getDateTimeProtobuf());
}