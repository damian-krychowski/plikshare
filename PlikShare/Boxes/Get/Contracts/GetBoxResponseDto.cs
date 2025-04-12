namespace PlikShare.Boxes.Get.Contracts;

public class GetBoxResponseDto
{
    public required BoxDetails Details { get; set; }
    public required List<Member> Members { get; set; }
    public required List<BoxLink> Links { get; set; }
    public required List<Subfolder>? Subfolders { get; set; }
    public required List<File>? Files { get; set; }

    public class BoxDetails
    {
        public required string ExternalId { get; set; }
        public required string Name { get; set; }
        public required bool IsEnabled { get; set; }
        public required Section? Header { get; set; }
        public required Section? Footer { get; set; }
        public required List<FolderItem> FolderPath { get; set; }
    }
    
    public class FolderItem
    {
        public required string ExternalId { get; set; }
        public required string Name { get; set; }
    }
    
    public class Section
    {
        public required bool IsEnabled { get; set; }
        public required string? Json { get; set; }
    }
    
    public class Member
    {
        public required string MemberExternalId { get; set; }
        public required string InviterEmail { get; set; }
        public required string MemberEmail { get; set; }
        public required bool WasInvitationAccepted { get; set; }
        public required Permissions Permissions { get; set; }
    }

    public class BoxLink
    {
        public required string ExternalId { get; set; }
        public required string AccessCode { get; set; }
        public required bool IsEnabled { get; set; }
        public required string Name { get; set; }
        public required Permissions Permissions { get; set; }
        public required List<string> WidgetOrigins { get; set; }
    }

    public class Permissions
    {
        public required bool AllowDownload { get; set; }
        public required bool AllowUpload { get; set; }
        public required bool AllowList { get; set; }
        public required bool AllowDeleteFile { get; set; }
        public required bool AllowRenameFile { get; set; }
        public required bool AllowMoveItems { get; set; }
        public required bool AllowCreateFolder { get; set; }
        public required bool AllowRenameFolder { get; set; }
        public required bool AllowDeleteFolder { get; set; }
    }

    public class Subfolder
    {
        public required string ExternalId { get; set; }
        public required string Name { get; set; }
    }
    
    public class File
    {
        public required string ExternalId { get; set; }
        public required string Name { get; set; }
        public required string Extension { get; set; }
        public required long SizeInBytes { get; set; }
    }
}
