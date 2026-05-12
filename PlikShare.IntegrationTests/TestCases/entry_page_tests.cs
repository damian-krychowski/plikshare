using FluentAssertions;
using PlikShare.EntryPage.Contracts;
using PlikShare.GeneralSettings;
using PlikShare.IntegrationTests.Infrastructure;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases;

[Collection(IntegrationTestsCollection.Name)]
public class entry_page_tests: TestFixture
{
    [Fact]
    public async Task entry_page_status_data_can_be_accessed_anonymously()
    {
        //when
        var entryPageSettings = await Api.EntryPage.GetSettings(
            cookie: null);

        //then
        entryPageSettings.Should().BeEquivalentTo(new GetEntryPageSettingsResponseDto
        {
            ApplicationSignUp = AppSettings.SignUpSetting.OnlyInvitedUsers.Value,
            PrivacyPolicyFilePath = null,
            TermsOfServiceFilePath = null,
            SignUpCheckboxes = [],
            SsoProviders = [],
            IsPasswordLoginEnabled = true,
            IsEmailNotificationsEnabled = default
        },
        opt => opt.Excluding(x => x.IsEmailNotificationsEnabled));
    }

    [Fact]
    public async Task entry_page_reports_email_notifications_enabled_when_provider_is_active()
    {
        //given — ensure there is an active email provider in the shared fixture state
        var appOwner = await SignIn(user: Users.AppOwner);
        await CreateAndActivateEmailProviderIfMissing(user: appOwner);

        //when
        var entryPageSettings = await Api.EntryPage.GetSettings(
            cookie: null);

        //then
        entryPageSettings.IsEmailNotificationsEnabled.Should().BeTrue(
            "an active email provider must surface as IsEmailNotificationsEnabled = true so the admin UI can offer the email delivery option");
    }

    [Fact]
    public async Task entry_page_reports_email_notifications_disabled_when_no_active_provider()
    {
        //given — sign in, ensure provider exists then deactivate it. Deactivate also wipes
        // the in-memory EmailProviderStore via the production endpoint path.
        var appOwner = await SignIn(user: Users.AppOwner);
        var provider = await CreateAndActivateEmailProviderIfMissing(user: appOwner);

        try
        {
            await Api.EmailProviders.Deactivate(
                emailProviderExternalId: provider.ExternalId,
                cookie: appOwner.Cookie,
                antiforgery: appOwner.Antiforgery);

            //when
            var entryPageSettings = await Api.EntryPage.GetSettings(
                cookie: null);

            //then
            entryPageSettings.IsEmailNotificationsEnabled.Should().BeFalse(
                "with no active email provider the admin UI must force the link delivery mode");
        }
        finally
        {
            // Restore active provider so other tests in the shared collection are not affected.
            await Api.EmailProviders.Activate(
                emailProviderExternalId: provider.ExternalId,
                cookie: appOwner.Cookie,
                antiforgery: appOwner.Antiforgery);
        }
    }

    public entry_page_tests(HostFixture8081 hostFixture, ITestOutputHelper testOutputHelper) : base(hostFixture, testOutputHelper)
    {
    }
}
