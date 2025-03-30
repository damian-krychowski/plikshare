using FluentAssertions;
using PlikShare.Boxes.UpdateFooter.Contracts;
using PlikShare.Boxes.UpdateFooterIsEnabled.Contracts;
using PlikShare.Boxes.UpdateHeader.Contracts;
using PlikShare.Boxes.UpdateHeaderIsEnabled.Contracts;
using PlikShare.BoxExternalAccess.Contracts;
using PlikShare.IntegrationTests.Infrastructure;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Boxes;

[Collection(IntegrationTestsCollection.Name)]
public class box_external_access_tests: TestFixture
{
    [Fact]
    public async Task by_default_box_html_is_empty()
    {
        //given
        var user = await SignIn(
            user: Users.AppOwner);
        
        //when
        var box = await CreateBox(
            user: user);

        //then
        var boxHtml = await Api.BoxExternalAccess.GetHtml(
            boxExternalId: box.ExternalId,
            cookie: user.Cookie);

        boxHtml.Should().BeEquivalentTo(new GetBoxHtmlResponseDto(
            HeaderHtml: null,
            FooterHtml: null));
    }
    
    [Fact]
    public async Task when_box_header_was_enabled_but_the_content_was_not_set_header_html_should_be_null()
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
        var boxHtml = await Api.BoxExternalAccess.GetHtml(
            boxExternalId: box.ExternalId,
            cookie: user.Cookie);

        boxHtml.Should().BeEquivalentTo(new GetBoxHtmlResponseDto(
            HeaderHtml: null,
            FooterHtml: null));
    }
    
    [Fact]
    public async Task when_box_header_was_enabled_and_content_was_set_header_html_should_be_available()
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
        
        await Api.Boxes.UpdateHeader(
            workspaceExternalId: box.WorkspaceExternalId,
            boxExternalId: box.ExternalId,
            request: new UpdateBoxHeaderRequestDto(
                Json: "new-header-json",
                Html: "new-header-html"),
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        //then
        var boxHtml = await Api.BoxExternalAccess.GetHtml(
            boxExternalId: box.ExternalId,
            cookie: user.Cookie);

        boxHtml.Should().BeEquivalentTo(new GetBoxHtmlResponseDto(
            HeaderHtml: "new-header-html",
            FooterHtml: null));
    }
    
    [Fact]
    public async Task when_box_header_content_was_set_but_it_was_not_enabled_then_header_html_should_be_null()
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
        var boxHtml = await Api.BoxExternalAccess.GetHtml(
            boxExternalId: box.ExternalId,
            cookie: user.Cookie);

        boxHtml.Should().BeEquivalentTo(new GetBoxHtmlResponseDto(
            HeaderHtml: null,
            FooterHtml: null));
    }
    
    [Fact]
    public async Task when_box_footer_was_enabled_but_the_content_was_not_set_footer_html_should_be_null()
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
        var boxHtml = await Api.BoxExternalAccess.GetHtml(
            boxExternalId: box.ExternalId,
            cookie: user.Cookie);

        boxHtml.Should().BeEquivalentTo(new GetBoxHtmlResponseDto(
            HeaderHtml: null,
            FooterHtml: null));
    }
    
    [Fact]
    public async Task when_box_footer_was_enabled_and_content_was_set_footer_html_should_be_available()
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
        
        await Api.Boxes.UpdateFooter(
            workspaceExternalId: box.WorkspaceExternalId,
            boxExternalId: box.ExternalId,
            request: new UpdateBoxFooterRequestDto(
                Json: "new-footer-json",
                Html: "new-footer-html"),
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        //then
        var boxHtml = await Api.BoxExternalAccess.GetHtml(
            boxExternalId: box.ExternalId,
            cookie: user.Cookie);

        boxHtml.Should().BeEquivalentTo(new GetBoxHtmlResponseDto(
            HeaderHtml: null,
            FooterHtml: "new-footer-html"));
    }
    
    [Fact]
    public async Task when_box_footer_content_was_set_but_it_was_not_enabled_then_footer_html_should_be_null()
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
        var boxHtml = await Api.BoxExternalAccess.GetHtml(
            boxExternalId: box.ExternalId,
            cookie: user.Cookie);

        boxHtml.Should().BeEquivalentTo(new GetBoxHtmlResponseDto(
            HeaderHtml: null,
            FooterHtml: null));
    }
    
    public box_external_access_tests(HostFixture8081 hostFixture, ITestOutputHelper testOutputHelper) : base(hostFixture, testOutputHelper)
    { 
    }
}