namespace PlikShare.Mcp.BoxLinks.Create.Contracts;

public class CreateBoxLinkResponseDto
{
    public required string ExternalId { get; init; }
    public required string AccessCode { get; init; }
}
