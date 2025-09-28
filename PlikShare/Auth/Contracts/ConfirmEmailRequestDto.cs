namespace PlikShare.Auth.Contracts;

public class ConfirmEmailRequestDto
{
    public required string UserExternalId {get; init;}
    public required string Code { get; init; }
}

public record ConfirmEmailResponseDto(string Code)
{
    public static ConfirmEmailResponseDto InvalidToken => new("invalid-token");
    public static ConfirmEmailResponseDto EmailConfirmed => new("email-confirmed");
};
