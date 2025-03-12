namespace PlikShare.Account.Contracts;

public record Get2FaStatusResponseDto(
    bool IsEnabled,
    int? RecoveryCodesLeft,
    string? QrCodeUri);