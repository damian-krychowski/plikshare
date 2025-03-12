namespace PlikShare.Auth.Contracts;

public record SignInUserRequestDto(
    string Email, 
    string Password, 
    bool RememberMe);

public record SignInUserResponseDto(string Code)
{
    public static SignInUserResponseDto Successful => new("signed-in");
    public static SignInUserResponseDto Failed => new("sign-in-failed");
    public static SignInUserResponseDto Required2Fa => new("2fa-required");
}