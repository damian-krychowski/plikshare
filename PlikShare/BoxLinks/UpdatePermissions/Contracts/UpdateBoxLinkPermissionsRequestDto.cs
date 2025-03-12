namespace PlikShare.BoxLinks.UpdatePermissions.Contracts;

public record UpdateBoxLinkPermissionsRequestDto(
    bool AllowDownload = false,
    bool AllowUpload = false,
    bool AllowList = false,
    bool AllowDeleteFile = false,
    bool AllowRenameFile = false,
    bool AllowMoveItems = false,
    bool AllowCreateFolder = false,
    bool AllowRenameFolder = false,
    bool AllowDeleteFolder = false);