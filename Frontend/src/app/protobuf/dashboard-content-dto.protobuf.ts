import * as protobuf from "protobufjs";

export function getDashboardContentDtoProtobuf() {
    const userType = new protobuf.Type("User")
        .add(new protobuf.Field("externalId", 1, "string"))
        .add(new protobuf.Field("email", 2, "string"));

    const workspacePermissionsType = new protobuf.Type("WorkspacePermissions")
        .add(new protobuf.Field("allowShare", 1, "bool"));

    const boxPermissionsType = new protobuf.Type("BoxPermissions")
        .add(new protobuf.Field("allowDownload", 1, "bool"))
        .add(new protobuf.Field("allowUpload", 2, "bool"))
        .add(new protobuf.Field("allowList", 3, "bool"))
        .add(new protobuf.Field("allowDeleteFile", 4, "bool"))
        .add(new protobuf.Field("allowRenameFile", 5, "bool"))
        .add(new protobuf.Field("allowMoveItems", 6, "bool"))
        .add(new protobuf.Field("allowCreateFolder", 7, "bool"))
        .add(new protobuf.Field("allowRenameFolder", 8, "bool"))
        .add(new protobuf.Field("allowDeleteFolder", 9, "bool"));

    const storageType = new protobuf.Type("Storage")
        .add(new protobuf.Field("externalId", 1, "string"))
        .add(new protobuf.Field("name", 2, "string"))
        .add(new protobuf.Field("type", 3, "string"))
        .add(new protobuf.Field("workspacesCount", 4, "int32"))
        .add(new protobuf.Field("encryptionType", 5, "string"));

    const workspaceDetailsType = new protobuf.Type("WorkspaceDetails")
        .add(new protobuf.Field("externalId", 1, "string"))
        .add(new protobuf.Field("name", 2, "string"))
        .add(new protobuf.Field("currentSizeInBytes", 3, "int64"))
        .add(new protobuf.Field("maxSizeInBytes", 4, "int64"))
        .add(new protobuf.Field("owner", 5, "User"))
        .add(new protobuf.Field("storageName", 6, "string"))
        .add(new protobuf.Field("permissions", 7, "WorkspacePermissions"))
        .add(new protobuf.Field("isUsedByIntegration", 8, "bool"))
        .add(new protobuf.Field("isBucketCreated", 9, "bool"));

    const workspaceInvitationType = new protobuf.Type("WorkspaceInvitation")
        .add(new protobuf.Field("workspaceExternalId", 1, "string"))
        .add(new protobuf.Field("workspaceName", 2, "string"))
        .add(new protobuf.Field("owner", 3, "User"))
        .add(new protobuf.Field("inviter", 4, "User"))
        .add(new protobuf.Field("permissions", 5, "WorkspacePermissions"))
        .add(new protobuf.Field("storageName", 6, "string"))
        .add(new protobuf.Field("isUsedByIntegration", 7, "bool"))
        .add(new protobuf.Field("isBucketCreated", 8, "bool"));

    const externalBoxType = new protobuf.Type("ExternalBox")
        .add(new protobuf.Field("boxExternalId", 1, "string"))
        .add(new protobuf.Field("boxName", 2, "string"))
        .add(new protobuf.Field("owner", 3, "User"))
        .add(new protobuf.Field("permissions", 4, "BoxPermissions"));

    const externalBoxInvitationType = new protobuf.Type("ExternalBoxInvitation")
        .add(new protobuf.Field("boxExternalId", 1, "string"))
        .add(new protobuf.Field("boxName", 2, "string"))
        .add(new protobuf.Field("owner", 3, "User"))
        .add(new protobuf.Field("inviter", 4, "User"))
        .add(new protobuf.Field("permissions", 5, "BoxPermissions"));

    const dashboardContentProtobuf = new protobuf.Type("GetDashboardContentResponseDto")
        .add(new protobuf.Field("storages", 1, "Storage", "repeated"))
        .add(new protobuf.Field("workspaces", 2, "WorkspaceDetails", "repeated"))
        .add(new protobuf.Field("workspaceInvitations", 3, "WorkspaceInvitation", "repeated"))
        .add(new protobuf.Field("otherWorkspaces", 4, "WorkspaceDetails", "repeated"))
        .add(new protobuf.Field("boxes", 5, "ExternalBox", "repeated"))
        .add(new protobuf.Field("boxInvitations", 6, "ExternalBoxInvitation", "repeated"))
        .add(userType)
        .add(workspacePermissionsType)
        .add(boxPermissionsType)
        .add(storageType)
        .add(workspaceDetailsType)
        .add(workspaceInvitationType)
        .add(externalBoxType)
        .add(externalBoxInvitationType);

    return dashboardContentProtobuf;
}