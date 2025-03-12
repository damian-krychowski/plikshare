namespace PlikShare.Auth.Contracts;

public record ConfirmEmailRequestDto(
    string UserExternalId, 
    string Code);

public record ConfirmEmailResponseDto(string Code)
{
    public static ConfirmEmailResponseDto InvalidToken => new("invalid-token");
    public static ConfirmEmailResponseDto EmailConfirmed => new("email-confirmed");
};
