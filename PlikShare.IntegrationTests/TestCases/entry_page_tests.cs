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
            SignUpCheckboxes = []
        });
    }
    
    public entry_page_tests(HostFixture8081 hostFixture, ITestOutputHelper testOutputHelper) : base(hostFixture, testOutputHelper)
    {
    }
}
