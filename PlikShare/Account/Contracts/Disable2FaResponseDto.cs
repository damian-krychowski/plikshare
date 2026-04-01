namespace PlikShare.Account.Contracts;

public record Disable2FaResponseDto(string Code)
{
    public static Disable2FaResponseDto Disabled => new("disabled");
    public static Disable2FaResponseDto Failed => new("failed");
    public static Disable2FaResponseDto NotAllowedForSsoUser => new("not-allowed-for-sso-user");
}