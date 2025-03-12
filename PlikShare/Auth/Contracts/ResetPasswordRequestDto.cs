namespace PlikShare.Auth.Contracts;

public record ResetPasswordRequestDto(
    string UserExternalId, 
    string Code, 
    string NewPassword);

public record ResetPasswordResponseDto(string Code)
{
    public static ResetPasswordResponseDto InvalidToken => new("invalid-token");
    public static ResetPasswordResponseDto PasswordReset => new("password-reset");
};
