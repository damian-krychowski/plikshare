namespace PlikShare.Account.Contracts;

public record GenerateRecoveryCodesResponseDto(
    List<string> RecoveryCodes);