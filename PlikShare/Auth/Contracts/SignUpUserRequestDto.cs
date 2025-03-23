namespace PlikShare.Auth.Contracts;

public class SignUpUserRequestDto
{
    public required string Email { get; init; }
    public required string Password{get; init; }
    public required string? InvitationCode{get; init; }
    public required List<int> SelectedCheckboxIds { get; init; }
}

public record SignUpUserResponseDto(string Code)
{
    public static SignUpUserResponseDto ConfirmationEmailSent => new("confirmation-email-sent");
    public static SignUpUserResponseDto InvitationRequired = new("invitation-required");
    public static SignUpUserResponseDto SignUpCheckboxesMissing = new("signed-up-checkboxes-missing");
    public static SignUpUserResponseDto SingedUpAndSignedIn = new("signed-up-and-signed-in");
}