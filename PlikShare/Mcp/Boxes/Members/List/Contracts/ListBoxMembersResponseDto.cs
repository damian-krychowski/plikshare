namespace PlikShare.Mcp.Boxes.Members.List.Contracts;

public class ListBoxMembersResponseDto
{
    public required List<BoxMemberDto> Members { get; init; }

    public class BoxMemberDto
    {
        public required string MemberExternalId { get; init; }
        public required string Email { get; init; }
        public required string InviterEmail { get; init; }
        public required bool InvitationAccepted { get; init; }
        public required BoxMemberPermissionsDto Permissions { get; init; }
    }

    public class BoxMemberPermissionsDto
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
