namespace PlikShare.Mcp.BoxLinks.RegenerateAccessCode.Contracts;

public class RegenerateBoxLinkAccessCodeResponseDto
{
    public required string BoxLinkExternalId { get; init; }
    public required string AccessCode { get; init; }
}
