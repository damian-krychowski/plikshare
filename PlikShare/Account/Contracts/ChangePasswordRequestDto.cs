namespace PlikShare.Account.Contracts;

public record ChangePasswordRequestDto(
    string CurrentPassword, 
    string NewPassword);

public record ChangePasswordResponseDto(string Code)
{
    public static ChangePasswordResponseDto Success => new ChangePasswordResponseDto("success");
    public static ChangePasswordResponseDto Failed => new ChangePasswordResponseDto("failed");
    public static ChangePasswordResponseDto PasswordMismatch => new ChangePasswordResponseDto("password-mismatch");
}