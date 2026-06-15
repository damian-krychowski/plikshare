namespace PlikShare.Agents.RotateToken.Contracts;

public class RotateAgentTokenResponseDto
{
    public required string Token { get; init; }
    public required string TokenMasked { get; init; }
}
