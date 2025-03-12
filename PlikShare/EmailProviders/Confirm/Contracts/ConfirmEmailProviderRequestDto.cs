namespace PlikShare.EmailProviders.Confirm.Contracts;

public record ConfirmEmailProviderRequestDto(
    string ConfirmationCode);