namespace PlikShare.Auth.Contracts;

public record SignInUser2FaRequestDto(
    string VerificationCode, 
    bool RememberMe, 
    bool RememberDevice);

public record SignInUser2FaResponseDto(string Code)
{
    public static SignInUser2FaResponseDto Successful => new("signed-in");
    public static SignInUser2FaResponseDto Failed => new("sign-in-failed");
    public static SignInUser2FaResponseDto InvalidVerificationCode => new("invalid-verification-code");
}