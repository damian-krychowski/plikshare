namespace PlikShare.Auth.Contracts;

public class SignUpUserRequestDto
{
    public required string Email { get; init; }
    public required string Password{get; init; }
    public required string? InvitationCode{get; init; }
    public required List<int> SelectedCheckboxIds { get; init; }
}

public record SignUpUserResponseDto(string Code, bool? HasPendingEphemeralEncryptionKeys = null)
{
    public static SignUpUserResponseDto ConfirmationEmailSent => new("confirmation-email-sent");
    public static SignUpUserResponseDto InvitationRequired = new("invitation-required");
    public static SignUpUserResponseDto SignUpCheckboxesMissing = new("signed-up-checkboxes-missing");
    public static SignUpUserResponseDto PasswordLoginDisabled = new("password-login-disabled");

    public static SignUpUserResponseDto SignedUpAndSignedIn(bool hasPendingEphemeralEncryptionKeys) =>
        new("signed-up-and-signed-in", hasPendingEphemeralEncryptionKeys);
}