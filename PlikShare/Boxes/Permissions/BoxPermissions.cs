namespace PlikShare.Boxes.Permissions;

public record BoxPermissions(
    bool AllowDownload, 
    bool AllowUpload, 
    bool AllowList, 
    bool AllowDeleteFile, 
    bool AllowRenameFile, 
    bool AllowMoveItems, 
    bool AllowCreateFolder, 
    bool AllowRenameFolder, 
    bool AllowDeleteFolder)
{
    public static BoxPermissions Full()
    {
        return new BoxPermissions(
            AllowDownload: true,
            AllowList: true,
            AllowDeleteFolder: true,
            AllowUpload: true,
            AllowDeleteFile: true,
            AllowMoveItems: true,
            AllowRenameFile: true,
            AllowRenameFolder: true,
            AllowCreateFolder: true);
    }

    public bool HasPermission(BoxPermission permission)
    {
        return permission switch
        {
            BoxPermission.AllowDownload => AllowDownload,
            BoxPermission.AllowUpload => AllowUpload,
            BoxPermission.AllowList => AllowList,
            BoxPermission.AllowDeleteFile => AllowDeleteFile,
            BoxPermission.AllowRenameFile => AllowRenameFile,
            BoxPermission.AllowMoveItems => AllowMoveItems,
            BoxPermission.AllowCreateFolder => AllowCreateFolder,
            BoxPermission.AllowRenameFolder => AllowRenameFolder,
            BoxPermission.AllowDeleteFolder => AllowDeleteFolder,

            _ => throw new ArgumentOutOfRangeException(nameof(permission), permission, null)
        };
    }
}

public enum BoxPermission
{
    AllowDownload,
    AllowUpload,
    AllowList,
    AllowDeleteFile,
    AllowRenameFile,
    AllowMoveItems,
    AllowCreateFolder,
    AllowRenameFolder,
    AllowDeleteFolder
}