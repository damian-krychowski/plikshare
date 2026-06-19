namespace PlikShare.Mcp.Boxes.Members.UpdatePermissions.Contracts;

public class UpdateBoxMemberPermissionsResponseDto
{
    public required string MemberExternalId { get; init; }
    public required PermissionsDto Permissions { get; init; }

    public class PermissionsDto
    {
        public required bool AllowDownload { get; init; }
        public required bool AllowUpload { get; init; }
        public required bool AllowList { get; init; }
        public required bool AllowDeleteFile { get; init; }
        public required bool AllowRenameFile { get; init; }
        public required bool AllowMoveItems { get; init; }
        public required bool AllowCreateFolder { get; init; }
        public required bool AllowRenameFolder { get; init; }
        public required bool AllowDeleteFolder { get; init; }
    }
}
