namespace PlikShare.Mcp.BoxLinks.Update;

public class UpdateBoxLinkParams
{
    public required string WorkspaceExternalId { get; init; }
    public required string BoxLinkExternalId { get; init; }
    public required string? Name { get; init; }
    public required bool? IsEnabled { get; init; }

    public required bool? AllowDownload { get; init; }
    public required bool? AllowUpload { get; init; }
    public required bool? AllowList { get; init; }
    public required bool? AllowDeleteFile { get; init; }
    public required bool? AllowRenameFile { get; init; }
    public required bool? AllowMoveItems { get; init; }
    public required bool? AllowCreateFolder { get; init; }
    public required bool? AllowRenameFolder { get; init; }
    public required bool? AllowDeleteFolder { get; init; }

    public required string[]? WidgetOrigins { get; init; }

    public bool HasPermissionChange =>
        AllowDownload is not null
        || AllowUpload is not null
        || AllowList is not null
        || AllowDeleteFile is not null
        || AllowRenameFile is not null
        || AllowMoveItems is not null
        || AllowCreateFolder is not null
        || AllowRenameFolder is not null
        || AllowDeleteFolder is not null;
}
