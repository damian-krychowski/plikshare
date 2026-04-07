using FluentAssertions;
using PlikShare.AuditLog;
using PlikShare.Boxes.Get.Contracts;
using PlikShare.BoxLinks.UpdateIsEnabled.Contracts;
using PlikShare.BoxLinks.UpdateName.Contracts;
using PlikShare.BoxLinks.UpdateWidgetOrigins.Contracts;
using PlikShare.IntegrationTests.Infrastructure;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Boxes;

[Collection(IntegrationTestsCollection.Name)]
public class box_link_management_tests : TestFixture
{
    private AppSignedInUser AppOwner { get; }

    public box_link_management_tests(
        HostFixture8081 hostFixture,
        ITestOutputHelper testOutputHelper) : base(hostFixture, testOutputHelper)
    {
        AppOwner = SignIn(user: Users.AppOwner).Result;
    }

    // --- Functional tests ---

    [Fact]
    public async Task when_box_link_name_is_updated_it_should_be_reflected_in_box_details()
    {
        //given
        var boxLink = await CreateBoxLink(user: AppOwner);

        //when
        await Api.BoxLinks.UpdateName(
            workspaceExternalId: boxLink.WorkspaceExternalId,
            boxLinkExternalId: boxLink.ExternalId,
            request: new UpdateBoxLinkNameRequestDto(Name: "renamed-link"),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var boxContent = await Api.Boxes.Get(
            workspaceExternalId: boxLink.WorkspaceExternalId,
            boxExternalId: boxLink.BoxExternalId,
            cookie: AppOwner.Cookie);

        boxContent.Links.Should().Contain(l =>
            l.ExternalId == boxLink.ExternalId.Value && l.Name == "renamed-link");
    }

    [Fact]
    public async Task when_box_link_widget_origins_are_updated_it_should_be_reflected_in_box_details()
    {
        //given
        var boxLink = await CreateBoxLink(user: AppOwner);

        //when
        await Api.BoxLinks.UpdateWidgetOrigins(
            workspaceExternalId: boxLink.WorkspaceExternalId,
            boxLinkExternalId: boxLink.ExternalId,
            request: new UpdateBoxLinkWidgetOriginsRequestDto
            {
                WidgetOrigins = ["https://example.com", "https://another.com"]
            },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var boxContent = await Api.Boxes.Get(
            workspaceExternalId: boxLink.WorkspaceExternalId,
            boxExternalId: boxLink.BoxExternalId,
            cookie: AppOwner.Cookie);

        var link = boxContent.Links.Should().Contain(l =>
            l.ExternalId == boxLink.ExternalId.Value).Which;

        link.WidgetOrigins.Should().BeEquivalentTo(
            ["https://example.com", "https://another.com"]);
    }

    [Fact]
    public async Task when_box_link_is_disabled_it_should_be_reflected_in_box_details()
    {
        //given
        var boxLink = await CreateBoxLink(user: AppOwner);

        //when
        await Api.BoxLinks.UpdateIsEnabled(
            workspaceExternalId: boxLink.WorkspaceExternalId,
            boxLinkExternalId: boxLink.ExternalId,
            request: new UpdateBoxLinkIsEnabledRequestDto(IsEnabled: false),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var boxContent = await Api.Boxes.Get(
            workspaceExternalId: boxLink.WorkspaceExternalId,
            boxExternalId: boxLink.BoxExternalId,
            cookie: AppOwner.Cookie);

        boxContent.Links.Should().Contain(l =>
            l.ExternalId == boxLink.ExternalId.Value && l.IsEnabled == false);
    }

    [Fact]
    public async Task when_box_link_access_code_is_regenerated_it_should_change()
    {
        //given
        var boxLink = await CreateBoxLink(user: AppOwner);
        var originalAccessCode = boxLink.AccessCode;

        //when
        var result = await Api.BoxLinks.RegenerateAccessCode(
            workspaceExternalId: boxLink.WorkspaceExternalId,
            boxLinkExternalId: boxLink.ExternalId,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        result.AccessCode.Should().NotBe(originalAccessCode);

        var boxContent = await Api.Boxes.Get(
            workspaceExternalId: boxLink.WorkspaceExternalId,
            boxExternalId: boxLink.BoxExternalId,
            cookie: AppOwner.Cookie);

        boxContent.Links.Should().Contain(l =>
            l.ExternalId == boxLink.ExternalId.Value &&
            l.AccessCode == result.AccessCode);
    }

    [Fact]
    public async Task when_box_link_is_deleted_it_should_not_be_visible_in_box_details()
    {
        //given
        var boxLink = await CreateBoxLink(user: AppOwner);

        //when
        await Api.BoxLinks.Delete(
            workspaceExternalId: boxLink.WorkspaceExternalId,
            boxLinkExternalId: boxLink.ExternalId,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var boxContent = await Api.Boxes.Get(
            workspaceExternalId: boxLink.WorkspaceExternalId,
            boxExternalId: boxLink.BoxExternalId,
            cookie: AppOwner.Cookie);

        boxContent.Links.Should().NotContain(l =>
            l.ExternalId == boxLink.ExternalId.Value);
    }

    // --- Audit log tests ---

    [Fact]
    public async Task updating_box_link_name_should_produce_audit_log_entry()
    {
        //given
        var boxLink = await CreateBoxLink(user: AppOwner);

        //when
        await Api.BoxLinks.UpdateName(
            workspaceExternalId: boxLink.WorkspaceExternalId,
            boxLinkExternalId: boxLink.ExternalId,
            request: new UpdateBoxLinkNameRequestDto(Name: "new-link-name"),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<AuditLogDetails.BoxLink.NameUpdated>(
            expectedEventType: AuditLogEventTypes.BoxLink.NameUpdated,
            assertDetails: details =>
            {
                details.WorkspaceExternalId.Should().Be(boxLink.WorkspaceExternalId);
                details.BoxExternalId.Should().Be(boxLink.BoxExternalId);
                details.ExternalId.Should().Be(boxLink.ExternalId);
                details.Name.Should().Be("new-link-name");
            },
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Info);
    }

    [Fact]
    public async Task updating_box_link_widget_origins_should_produce_audit_log_entry()
    {
        //given
        var boxLink = await CreateBoxLink(user: AppOwner);
        var widgetOrigins = new List<string> { "https://example.com", "https://another.com" };

        //when
        await Api.BoxLinks.UpdateWidgetOrigins(
            workspaceExternalId: boxLink.WorkspaceExternalId,
            boxLinkExternalId: boxLink.ExternalId,
            request: new UpdateBoxLinkWidgetOriginsRequestDto
            {
                WidgetOrigins = widgetOrigins
            },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<AuditLogDetails.BoxLink.WidgetOriginsUpdated>(
            expectedEventType: AuditLogEventTypes.BoxLink.WidgetOriginsUpdated,
            assertDetails: details =>
            {
                details.WorkspaceExternalId.Should().Be(boxLink.WorkspaceExternalId);
                details.BoxExternalId.Should().Be(boxLink.BoxExternalId);
                details.ExternalId.Should().Be(boxLink.ExternalId);
                details.WidgetOrigins.Should().BeEquivalentTo(widgetOrigins);
            },
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Info);
    }

    [Fact]
    public async Task updating_box_link_is_enabled_should_produce_audit_log_entry()
    {
        //given
        var boxLink = await CreateBoxLink(user: AppOwner);

        //when
        await Api.BoxLinks.UpdateIsEnabled(
            workspaceExternalId: boxLink.WorkspaceExternalId,
            boxLinkExternalId: boxLink.ExternalId,
            request: new UpdateBoxLinkIsEnabledRequestDto(IsEnabled: false),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<AuditLogDetails.BoxLink.IsEnabledUpdated>(
            expectedEventType: AuditLogEventTypes.BoxLink.IsEnabledUpdated,
            assertDetails: details =>
            {
                details.WorkspaceExternalId.Should().Be(boxLink.WorkspaceExternalId);
                details.BoxExternalId.Should().Be(boxLink.BoxExternalId);
                details.ExternalId.Should().Be(boxLink.ExternalId);
                details.IsEnabled.Should().BeFalse();
            },
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Info);
    }

    [Fact]
    public async Task regenerating_box_link_access_code_should_produce_audit_log_entry()
    {
        //given
        var boxLink = await CreateBoxLink(user: AppOwner);

        //when
        await Api.BoxLinks.RegenerateAccessCode(
            workspaceExternalId: boxLink.WorkspaceExternalId,
            boxLinkExternalId: boxLink.ExternalId,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<AuditLogDetails.BoxLink.AccessCodeRegenerated>(
            expectedEventType: AuditLogEventTypes.BoxLink.AccessCodeRegenerated,
            assertDetails: details =>
            {
                details.WorkspaceExternalId.Should().Be(boxLink.WorkspaceExternalId);
                details.BoxExternalId.Should().Be(boxLink.BoxExternalId);
                details.ExternalId.Should().Be(boxLink.ExternalId);
            },
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Warning);
    }

    [Fact]
    public async Task deleting_box_link_should_produce_audit_log_entry()
    {
        //given
        var boxLink = await CreateBoxLink(user: AppOwner);

        //when
        await Api.BoxLinks.Delete(
            workspaceExternalId: boxLink.WorkspaceExternalId,
            boxLinkExternalId: boxLink.ExternalId,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<AuditLogDetails.BoxLink.Deleted>(
            expectedEventType: AuditLogEventTypes.BoxLink.Deleted,
            assertDetails: details =>
            {
                details.WorkspaceExternalId.Should().Be(boxLink.WorkspaceExternalId);
                details.BoxExternalId.Should().Be(boxLink.BoxExternalId);
                details.ExternalId.Should().Be(boxLink.ExternalId);
            },
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Warning);
    }
}
