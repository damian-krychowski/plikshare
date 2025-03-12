using PlikShare.Core.Utils;

namespace PlikShare.Account.Contracts;

public record Enable2FaRequestDto(
    string VerificationCode);

public record Enable2FaResponseDto(
    string Code,
    List<string> RecoveryCodes)
{
    public const string EnabledCode = "enabled";
    public static Enable2FaResponseDto Enabled(IEnumerable<string> recoveryCodes) => new(EnabledCode, recoveryCodes.AsList());
    public static Enable2FaResponseDto InvalidVerificationCode => new("invalid-verification-code", []);

    public static Enable2FaResponseDto Failed => new("failed", []);
}