using PlikShare.EntryPage.Contracts;
using PlikShare.GeneralSettings;

namespace PlikShare.EntryPage;

public static class EntryPageEndpoints
{
    public static void MapEntryPageEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/entry-page")
            .WithTags("Entry Page")
            .AllowAnonymous();

        group.MapGet("/", GetEntryPageSettings)
            .WithName("GetEntryPageSettings");
    }

    private static GetEntryPageSettingsResponseDto GetEntryPageSettings(
        AppSettings appSettings)
    {
        return new GetEntryPageSettingsResponseDto
        {
            ApplicationSignUp = appSettings.ApplicationSignUp.Value,

            TermsOfServiceFilePath = appSettings.TermsOfService.FileName is null
                ? null
                : "api/legal-files/terms-of-service",

            PrivacyPolicyFilePath = appSettings.PrivacyPolicy.FileName is null
                ? null
                : "api/legal-files/privacy-policy",
        };
    }
}