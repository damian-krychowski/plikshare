namespace PlikShare.Mcp.BoxAccess.GetDetails.Contracts;

public class GetBoxDetailsResponseDto
{
    public required string ExternalId { get; init; }
    public required string Name { get; init; }

    /// <summary>
    /// A disabled box exposes no content — listing, reading, uploading and the rest are unavailable until
    /// an operator re-enables it.
    /// </summary>
    public required bool IsEnabled { get; init; }

    /// <summary>
    /// External id of the root folder the box exposes. Pass it (or omit it) to list_box_content to browse
    /// the box from its root. Null only when the box has lost its folder, which also means it is off.
    /// </summary>
    public required string? RootFolderExternalId { get; init; }
}
