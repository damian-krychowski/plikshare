namespace PlikShare.Mcp.ShareLinks.Create.Contracts;

public class CreateShareLinkResponseDto
{
    public required string ExternalId { get; init; }
    public required string Url { get; init; }
}
