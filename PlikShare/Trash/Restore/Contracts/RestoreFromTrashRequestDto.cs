using PlikShare.Files.Id;
using PlikShare.Folders.Id;

namespace PlikShare.Trash.Restore.Contracts;

public class RestoreFromTrashRequestDto
{
    public required List<RestoreItemDto> Items { get; init; }
}

public enum RestoreMode
{
    OriginalPath = 0,
    ChosenFolder = 1
}

public class RestoreItemDto
{
    public required FileExtId FileExternalId { get; init; }

    public required RestoreMode Mode { get; init; }

    /// <summary>
    /// Required when <see cref="Mode"/> is <see cref="RestoreMode.ChosenFolder"/> — the folder the
    /// file should be placed in. Null/ignored otherwise (snapshot decides the path).
    /// </summary>
    public required FolderExtId? TargetFolderExternalId { get; init; }
}

public class RestoreFromTrashResponseDto
{
    public required List<RestoreItemResultDto> Results { get; init; }
}

public enum RestoreStatus
{
    Restored = 0,
    NotFound = 1,
    DestinationInvalid = 2
}

public class RestoreItemResultDto
{
    public required FileExtId FileExternalId { get; init; }

    public required RestoreStatus Status { get; init; }
}
