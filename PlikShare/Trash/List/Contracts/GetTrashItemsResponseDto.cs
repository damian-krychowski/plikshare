using PlikShare.Files.Id;

namespace PlikShare.Trash.List.Contracts;

public class GetTrashItemsResponseDto
{
    public required List<TrashItemDto> Items { get; init; }
    public required long TotalSizeInBytes { get; init; }
}

public class TrashItemDto
{
    public required FileExtId ExternalId { get; init; }
    public required string Name { get; init; }
    public required string Extension { get; init; }
    public required long SizeInBytes { get; init; }
    public required DateTimeOffset DeletedAt { get; init; }

    /// <summary>
    /// When the sweeper is expected to permanently remove this item. Null only when the
    /// workspace policy keeps trash forever (enabled, no retention limit). For a disabled
    /// policy this equals <see cref="DeletedAt"/> — the item is purged at the next sweep.
    /// </summary>
    public required DateTimeOffset? AutoDeletesAt { get; init; }

    /// <summary>
    /// Folder names root → leaf where the file lived before being trashed — decoded plaintext,
    /// ready for display. Null for files trashed directly from workspace root.
    /// </summary>
    public required List<string>? OriginalFolderPath { get; init; }
}
