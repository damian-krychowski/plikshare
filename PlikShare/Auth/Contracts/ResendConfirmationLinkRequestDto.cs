namespace PlikShare.Auth.Contracts;

public record ResendConfirmationLinkRequestDto(
    string Email);

public record ResendConfirmationLinkResponseDto(string Code)
{
    public static ResendConfirmationLinkResponseDto ConfirmationEmailSent => new("confirmation-email-sent");
}