using PlikShare.GeneralSettings;

namespace PlikShare.EntryPage.Contracts;

public class GetEntryPageSettingsResponseDto
{
    public required string ApplicationSignUp { get; init; }
    public required string? TermsOfServiceFilePath { get; init; }
    public required string? PrivacyPolicyFilePath { get; init; }
    public required List<AppSettings.SignUpCheckbox> SignUpCheckboxes { get; init; }
}