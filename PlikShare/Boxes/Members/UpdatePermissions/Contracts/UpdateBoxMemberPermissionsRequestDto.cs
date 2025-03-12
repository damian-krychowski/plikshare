namespace PlikShare.Boxes.Members.UpdatePermissions.Contracts;

public class UpdateBoxMemberPermissionsRequestDto
{
    public bool AllowDownload { get; set; }
    public bool AllowUpload { get; set; }
    public bool AllowList { get; set; }
    public bool AllowDeleteFile { get; set; }
    public bool AllowRenameFile { get; set; }
    public bool AllowMoveItems { get; set; }
    public bool AllowCreateFolder { get; set; }
    public bool AllowRenameFolder { get; set; }
    public bool AllowDeleteFolder { get; set; }
}