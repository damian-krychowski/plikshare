import * as protobuf from "protobufjs";

export function getSearchResponseDtoProtobuf() {
    const folderAncestorType = new protobuf.Type("FolderAncestor")
        .add(new protobuf.Field("externalId", 1, "string"))
        .add(new protobuf.Field("name", 2, "string"));

    const workspaceType = new protobuf.Type("Workspace")
        .add(new protobuf.Field("externalId", 1, "string"))
        .add(new protobuf.Field("name", 2, "string"))
        .add(new protobuf.Field("currentSizeInBytes", 3, "int64"))
        .add(new protobuf.Field("maxSizeInBytes", 4, "int64"))
        .add(new protobuf.Field("ownerEmail", 5, "string"))
        .add(new protobuf.Field("ownerExternalId", 6, "string"))
        .add(new protobuf.Field("isOwnedByUser", 7, "bool"))
        .add(new protobuf.Field("allowShare", 8, "bool"))
        .add(new protobuf.Field("isUsedByIntegration", 9, "bool"))
        .add(new protobuf.Field("isBucketCreated", 10, "bool"));

    const workspaceFolderType = new protobuf.Type("WorkspaceFolder")
        .add(new protobuf.Field("externalId", 1, "string"))
        .add(new protobuf.Field("name", 2, "string"))
        .add(new protobuf.Field("workspaceExternalId", 3, "string"))
        .add(new protobuf.Field("ancestors", 4, "FolderAncestor", "repeated"));

    const workspaceBoxType = new protobuf.Type("WorkspaceBox")
        .add(new protobuf.Field("externalId", 1, "string"))
        .add(new protobuf.Field("name", 2, "string"))
        .add(new protobuf.Field("workspaceExternalId", 3, "string"))
        .add(new protobuf.Field("isEnabled", 4, "bool"))
        .add(new protobuf.Field("folderPath", 5, "FolderAncestor", "repeated"));

    const workspaceFileType = new protobuf.Type("WorkspaceFile")
        .add(new protobuf.Field("externalId", 1, "string"))
        .add(new protobuf.Field("name", 2, "string"))
        .add(new protobuf.Field("workspaceExternalId", 3, "string"))
        .add(new protobuf.Field("sizeInBytes", 4, "int64"))
        .add(new protobuf.Field("extension", 5, "string"))
        .add(new protobuf.Field("folderPath", 6, "FolderAncestor", "repeated"));

    const externalBoxType = new protobuf.Type("ExternalBox")
        .add(new protobuf.Field("externalId", 1, "string"))
        .add(new protobuf.Field("name", 2, "string"))
        .add(new protobuf.Field("ownerEmail", 3, "string"))
        .add(new protobuf.Field("ownerExternalId", 4, "string"))
        .add(new protobuf.Field("allowDownload", 5, "bool"))
        .add(new protobuf.Field("allowUpload", 6, "bool"))
        .add(new protobuf.Field("allowList", 7, "bool"))
        .add(new protobuf.Field("allowDeleteFile", 8, "bool"))
        .add(new protobuf.Field("allowRenameFile", 9, "bool"))
        .add(new protobuf.Field("allowMoveItems", 10, "bool"))
        .add(new protobuf.Field("allowCreateFolder", 11, "bool"))
        .add(new protobuf.Field("allowDeleteFolder", 12, "bool"))
        .add(new protobuf.Field("allowRenameFolder", 13, "bool"));

    const externalBoxFolderType = new protobuf.Type("ExternalBoxFolder")
        .add(new protobuf.Field("externalId", 1, "string"))
        .add(new protobuf.Field("name", 2, "string"))
        .add(new protobuf.Field("boxExternalId", 3, "string"))
        .add(new protobuf.Field("ancestors", 4, "FolderAncestor", "repeated"));

    const externalBoxFileType = new protobuf.Type("ExternalBoxFile")
        .add(new protobuf.Field("externalId", 1, "string"))
        .add(new protobuf.Field("name", 2, "string"))
        .add(new protobuf.Field("boxExternalId", 3, "string"))
        .add(new protobuf.Field("sizeInBytes", 4, "int64"))
        .add(new protobuf.Field("extension", 5, "string"))
        .add(new protobuf.Field("folderPath", 6, "FolderAncestor", "repeated"))
        .add(new protobuf.Field("wasUploadedByUser", 7, "bool"));

    const workspaceGroupType = new protobuf.Type("WorkspaceGroup")
        .add(new protobuf.Field("externalId", 1, "string"))
        .add(new protobuf.Field("name", 2, "string"))
        .add(new protobuf.Field("allowShare", 3, "bool"))
        .add(new protobuf.Field("isOwnedByUser", 4, "bool"));

    const externalBoxGroupType = new protobuf.Type("ExternalBoxGroup")
        .add(new protobuf.Field("externalId", 1, "string"))
        .add(new protobuf.Field("name", 2, "string"))
        .add(new protobuf.Field("allowDownload", 3, "bool"))
        .add(new protobuf.Field("allowUpload", 4, "bool"))
        .add(new protobuf.Field("allowList", 5, "bool"))
        .add(new protobuf.Field("allowDeleteFile", 6, "bool"))
        .add(new protobuf.Field("allowRenameFile", 7, "bool"))
        .add(new protobuf.Field("allowMoveItems", 8, "bool"))
        .add(new protobuf.Field("allowCreateFolder", 9, "bool"))
        .add(new protobuf.Field("allowDeleteFolder", 10, "bool"))
        .add(new protobuf.Field("allowRenameFolder", 11, "bool"));

    const searchResponseDtoProtobuf = new protobuf.Type("SearchResponseDto")
        .add(new protobuf.Field("workspaceGroups", 1, "WorkspaceGroup", "repeated"))
        .add(new protobuf.Field("externalBoxGroups", 2, "ExternalBoxGroup", "repeated"))
        .add(new protobuf.Field("workspaces", 3, "Workspace", "repeated"))
        .add(new protobuf.Field("workspaceFolders", 4, "WorkspaceFolder", "repeated"))
        .add(new protobuf.Field("workspaceBoxes", 5, "WorkspaceBox", "repeated"))
        .add(new protobuf.Field("workspaceFiles", 6, "WorkspaceFile", "repeated"))
        .add(new protobuf.Field("externalBoxes", 7, "ExternalBox", "repeated"))
        .add(new protobuf.Field("externalBoxFolders", 8, "ExternalBoxFolder", "repeated"))
        .add(new protobuf.Field("externalBoxFiles", 9, "ExternalBoxFile", "repeated"))
        .add(folderAncestorType)
        .add(workspaceType)
        .add(workspaceFolderType)
        .add(workspaceBoxType)
        .add(workspaceFileType)
        .add(externalBoxType)
        .add(externalBoxFolderType)
        .add(externalBoxFileType)
        .add(workspaceGroupType)
        .add(externalBoxGroupType);

    return searchResponseDtoProtobuf;
}