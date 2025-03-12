namespace PlikShare.Auth.Contracts;

public record SignInUserRecoveryCodeRequestDto(
    string RecoveryCode);

public record SignInUserRecoveryCodeResponseDto(string Code)
{
    public static SignInUserRecoveryCodeResponseDto Successful => new("signed-in");
    public static SignInUserRecoveryCodeResponseDto Failed => new("sign-in-failed");
    public static SignInUserRecoveryCodeResponseDto InvalidRecoveryCode => new("invalid-recovery-code");
}