namespace PlikShare.Search.Get.Contracts;

public class SearchRequestDto
{
    public required string[] WorkspaceExternalIds { get; init; }
    public required string[] BoxExternalIds { get; init; }
    public required string Phrase { get; init; }
}