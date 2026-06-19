namespace PlikShare.Mcp.BoxLinks.List.Contracts;

public class ListBoxLinksResponseDto
{
    public required List<BoxLinkDto> Links { get; init; }

    public class BoxLinkDto
    {
        public required string ExternalId { get; init; }
        public required string Name { get; init; }
        public required bool IsEnabled { get; init; }
        public required string AccessCode { get; init; }
        public required BoxLinkPermissionsDto Permissions { get; init; }
        public required List<string> WidgetOrigins { get; init; }
    }

    public class BoxLinkPermissionsDto
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
