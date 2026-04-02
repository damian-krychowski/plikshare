using PlikShare.GeneralSettings;

namespace PlikShare.EntryPage.Contracts;

public class GetEntryPageSettingsResponseDto
{
    public required bool IsPasswordLoginEnabled { get; init; }
    public required string ApplicationSignUp { get; init; }
    public required string? TermsOfServiceFilePath { get; init; }
    public required string? PrivacyPolicyFilePath { get; init; }
    public required List<AppSettings.SignUpCheckbox> SignUpCheckboxes { get; init; }
    public required List<SsoProviderDto> SsoProviders { get; init; }
}

public class SsoProviderDto
{
    public required string ExternalId { get; init; }
    public required string Name { get; init; }
    public required string Type { get; init; }
}