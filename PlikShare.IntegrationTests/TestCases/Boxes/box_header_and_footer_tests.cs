using FluentAssertions;
using PlikShare.AuditLog;
using PlikShare.Boxes.Get.Contracts;
using PlikShare.Boxes.UpdateFooter.Contracts;
using PlikShare.Boxes.UpdateFooterIsEnabled.Contracts;
using PlikShare.Boxes.UpdateHeader.Contracts;
using PlikShare.Boxes.UpdateHeaderIsEnabled.Contracts;
using PlikShare.IntegrationTests.Infrastructure;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Boxes;

[Collection(IntegrationTestsCollection.Name)]
public class box_header_and_footer_tests: TestFixture
{
    [Fact]
    public async Task by_default_box_header_and_footer_are_disabled()
    {
        //given
        var user = await SignIn(
            user: Users.AppOwner);
        
        //when
        var box = await CreateBox(
            user: user);

        //then
        var boxContent = await Api.Boxes.Get(
            workspaceExternalId: box.WorkspaceExternalId,
            boxExternalId: box.ExternalId,
            cookie: user.Cookie);

        boxContent.Details.Header.Should().BeEquivalentTo(new GetBoxResponseDto.Section()
        {
            IsEnabled = false,
            Json = null!
        });

        boxContent.Details.Footer.Should().BeEquivalentTo(new GetBoxResponseDto.Section
        {
            IsEnabled = false,
            Json = null!
        });
    }
    
    [Fact]
    public async Task can_enable_box_header()
    {
        //given
        var user = await SignIn(
            user: Users.AppOwner);
        
        var box = await CreateBox(
            user: user);
        
        //when
        await Api.Boxes.UpdateHeaderIsEnabled(
            workspaceExternalId: box.WorkspaceExternalId,
            boxExternalId: box.ExternalId,
            request: new UpdateBoxHeaderIsEnabledRequestDto(IsEnabled: true),
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        //then
        var boxContent = await Api.Boxes.Get(
            workspaceExternalId: box.WorkspaceExternalId,
            boxExternalId: box.ExternalId,
            cookie: user.Cookie);

        boxContent.Details.Header.Should().BeEquivalentTo(new GetBoxResponseDto.Section
        {
            IsEnabled = true,
            Json = null
        });
        
        boxContent.Details.Footer.Should().BeEquivalentTo(new GetBoxResponseDto.Section
        {
            IsEnabled = false,
            Json = null
        });
    }
    
    [Fact]
    public async Task can_enable_box_footer()
    {
        //given
        var user = await SignIn(
            user: Users.AppOwner);
        
        var box = await CreateBox(
            user: user);
        
        //when
        await Api.Boxes.UpdateFooterIsEnabled(
            workspaceExternalId: box.WorkspaceExternalId,
            boxExternalId: box.ExternalId,
            request: new UpdateBoxFooterIsEnabledRequestDto(IsEnabled: true),
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        //then
        var boxContent = await Api.Boxes.Get(
            workspaceExternalId: box.WorkspaceExternalId,
            boxExternalId: box.ExternalId,
            cookie: user.Cookie);

        boxContent.Details.Header.Should().BeEquivalentTo(new GetBoxResponseDto.Section
        {
            IsEnabled = false,
            Json = null
        });
        
        boxContent.Details.Footer.Should().BeEquivalentTo(new GetBoxResponseDto.Section
        {
            IsEnabled = true,
            Json = null
        });
    }

    [Fact]
    public async Task can_set_header_content()
    {
        //given
        var user = await SignIn(
            user: Users.AppOwner);
        
        var box = await CreateBox(
            user: user);
        
        //when
        await Api.Boxes.UpdateHeader(
            workspaceExternalId: box.WorkspaceExternalId,
            boxExternalId: box.ExternalId,
            request: new UpdateBoxHeaderRequestDto(
                Json: "new-header-json",
                Html: "new-header-html"),
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);
        
        //then
        var boxContent = await Api.Boxes.Get(
            workspaceExternalId: box.WorkspaceExternalId,
            boxExternalId: box.ExternalId,
            cookie: user.Cookie);
        
        boxContent.Details.Header.Should().BeEquivalentTo(new GetBoxResponseDto.Section
        {
            IsEnabled = false,
            Json = "new-header-json"
        });
        
        boxContent.Details.Footer.Should().BeEquivalentTo(new GetBoxResponseDto.Section
        {
            IsEnabled = false,
            Json = null
        });
    }
    
    [Fact]
    public async Task can_set_footer_content()
    {
        //given
        var user = await SignIn(
            user: Users.AppOwner);
        
        var box = await CreateBox(
            user: user);
        
        //when
        await Api.Boxes.UpdateFooter(
            workspaceExternalId: box.WorkspaceExternalId,
            boxExternalId: box.ExternalId,
            request: new UpdateBoxFooterRequestDto(
                Json: "new-footer-json",
                Html: "new-footer-html"),
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);
        
        //then
        var boxContent = await Api.Boxes.Get(
            workspaceExternalId: box.WorkspaceExternalId,
            boxExternalId: box.ExternalId,
            cookie: user.Cookie);
        
        boxContent.Details.Header.Should().BeEquivalentTo(new GetBoxResponseDto.Section
        {
            IsEnabled = false,
            Json = null
        });
        
        boxContent.Details.Footer.Should().BeEquivalentTo(new GetBoxResponseDto.Section
        {
            IsEnabled = false,
            Json = "new-footer-json"
        });
    }
    
    // --- Audit log tests ---

    [Fact]
    public async Task enabling_box_header_should_produce_audit_log_entry()
    {
        //given
        var user = await SignIn(
            user: Users.AppOwner);

        var box = await CreateBox(
            user: user);

        //when
        await Api.Boxes.UpdateHeaderIsEnabled(
            workspaceExternalId: box.WorkspaceExternalId,
            boxExternalId: box.ExternalId,
            request: new UpdateBoxHeaderIsEnabledRequestDto(IsEnabled: true),
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        //then
        await AssertAuditLogContains<AuditLogDetails.Box.HeaderIsEnabledUpdated>(
            expectedEventType: AuditLogEventTypes.Box.HeaderIsEnabledUpdated,
            assertDetails: details =>
            {
                details.WorkspaceExternalId.Should().Be(box.WorkspaceExternalId);
                details.ExternalId.Should().Be(box.ExternalId);
                details.IsEnabled.Should().BeTrue();
            },
            expectedActorEmail: user.Email,
            expectedSeverity: AuditLogSeverities.Info);
    }

    [Fact]
    public async Task setting_box_header_content_should_produce_audit_log_entry()
    {
        //given
        var user = await SignIn(
            user: Users.AppOwner);

        var box = await CreateBox(
            user: user);

        //when
        await Api.Boxes.UpdateHeader(
            workspaceExternalId: box.WorkspaceExternalId,
            boxExternalId: box.ExternalId,
            request: new UpdateBoxHeaderRequestDto(
                Json: "header-json",
                Html: "header-html"),
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        //then
        await AssertAuditLogContains<AuditLogDetails.Box.HeaderUpdated>(
            expectedEventType: AuditLogEventTypes.Box.HeaderUpdated,
            assertDetails: details =>
            {
                details.WorkspaceExternalId.Should().Be(box.WorkspaceExternalId);
                details.ExternalId.Should().Be(box.ExternalId);
            },
            expectedActorEmail: user.Email,
            expectedSeverity: AuditLogSeverities.Info);
    }

    [Fact]
    public async Task enabling_box_footer_should_produce_audit_log_entry()
    {
        //given
        var user = await SignIn(
            user: Users.AppOwner);

        var box = await CreateBox(
            user: user);

        //when
        await Api.Boxes.UpdateFooterIsEnabled(
            workspaceExternalId: box.WorkspaceExternalId,
            boxExternalId: box.ExternalId,
            request: new UpdateBoxFooterIsEnabledRequestDto(IsEnabled: true),
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        //then
        await AssertAuditLogContains<AuditLogDetails.Box.FooterIsEnabledUpdated>(
            expectedEventType: AuditLogEventTypes.Box.FooterIsEnabledUpdated,
            assertDetails: details =>
            {
                details.WorkspaceExternalId.Should().Be(box.WorkspaceExternalId);
                details.ExternalId.Should().Be(box.ExternalId);
                details.IsEnabled.Should().BeTrue();
            },
            expectedActorEmail: user.Email,
            expectedSeverity: AuditLogSeverities.Info);
    }

    [Fact]
    public async Task setting_box_footer_content_should_produce_audit_log_entry()
    {
        //given
        var user = await SignIn(
            user: Users.AppOwner);

        var box = await CreateBox(
            user: user);

        //when
        await Api.Boxes.UpdateFooter(
            workspaceExternalId: box.WorkspaceExternalId,
            boxExternalId: box.ExternalId,
            request: new UpdateBoxFooterRequestDto(
                Json: "footer-json",
                Html: "footer-html"),
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        //then
        await AssertAuditLogContains<AuditLogDetails.Box.FooterUpdated>(
            expectedEventType: AuditLogEventTypes.Box.FooterUpdated,
            assertDetails: details =>
            {
                details.WorkspaceExternalId.Should().Be(box.WorkspaceExternalId);
                details.ExternalId.Should().Be(box.ExternalId);
            },
            expectedActorEmail: user.Email,
            expectedSeverity: AuditLogSeverities.Info);
    }

    public box_header_and_footer_tests(HostFixture8081 hostFixture, ITestOutputHelper testOutputHelper) : base(hostFixture, testOutputHelper)
    {
    }
}