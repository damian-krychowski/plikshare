namespace PlikShare.Auth.Contracts;

public record SignUpUserRequestDto(
    string Email, 
    string Password, 
    string? InvitationCode);

public record SignUpUserResponseDto(string Code)
{
    public static SignUpUserResponseDto ConfirmationEmailSent => new("confirmation-email-sent");
    public static SignUpUserResponseDto InvitationRequired = new("invitation-required");
    public static SignUpUserResponseDto SingedUpAndSignedIn = new("signed-up-and-signed-in");
}