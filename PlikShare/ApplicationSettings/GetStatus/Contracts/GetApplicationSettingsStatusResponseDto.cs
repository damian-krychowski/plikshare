namespace PlikShare.ApplicationSettings.GetStatus.Contracts;

public class GetApplicationSettingsStatusResponseDto
{
    public required bool? IsEmailProviderConfigured { get; init; }
    public required bool? IsStorageConfigured { get; init; }
}