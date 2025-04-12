using FluentAssertions;
using PlikShare.Boxes.CreateLink.Contracts;
using PlikShare.Boxes.Get.Contracts;
using PlikShare.BoxLinks.UpdatePermissions.Contracts;
using PlikShare.IntegrationTests.Infrastructure;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Boxes;

[Collection(IntegrationTestsCollection.Name)]
public class box_link_tests: TestFixture
{
    [Fact]
    public async Task when_box_link_is_created_it_is_visible_in_box_details_page()
    {
        //given
        var user = await SignIn(
            user: Users.AppOwner);

        var box = await CreateBox(
            user: user);
        
        //when
        var boxLink = await Api.Boxes.CreateBoxLink(
            workspaceExternalId: box.WorkspaceExternalId,
            boxExternalId: box.ExternalId,
            request: new CreateBoxLinkRequestDto(
                Name: "my first box link"),
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        //then
        var boxContent = await Api.Boxes.Get(
            workspaceExternalId: box.WorkspaceExternalId,
            boxExternalId: box.ExternalId,
            cookie: user.Cookie);

        boxContent.Links.Should().BeEquivalentTo([
            new GetBoxResponseDto.BoxLink
            {
                ExternalId = boxLink.ExternalId.Value,
                AccessCode = boxLink.AccessCode,
                IsEnabled = true,
                Name = "my first box link",
                Permissions = new GetBoxResponseDto.Permissions
                {
                    AllowList = true,

                    AllowDeleteFile = false,
                    AllowCreateFolder = false,
                    AllowRenameFile = false,
                    AllowMoveItems = false,
                    AllowRenameFolder = false,
                    AllowDeleteFolder = false,
                    AllowUpload = false,
                    AllowDownload = false,
                },
                WidgetOrigins = []
            }
        ]);
    }
    
    [Fact]
    public async Task can_update_box_link_permissions()
    {
        //given
        var user = await SignIn(
            user: Users.AppOwner);

        var boxLink = await CreateBoxLink(
            user: user);
        
        //when
        await Api.BoxLinks.UpdatePermissions(
            workspaceExternalId: boxLink.WorkspaceExternalId,
            boxLinkExternalId: boxLink.ExternalId,
            request: new UpdateBoxLinkPermissionsRequestDto(
                AllowUpload: true,
                AllowList: true,
                AllowCreateFolder: true,
                AllowDeleteFile: true,
                AllowDeleteFolder: true,
                AllowMoveItems: true,
                AllowRenameFile: true,
                AllowRenameFolder: true,
                AllowDownload: true),
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        //then
        var boxContent = await Api.Boxes.Get(
            workspaceExternalId: boxLink.WorkspaceExternalId,
            boxExternalId: boxLink.BoxExternalId,
            cookie: user.Cookie);

        boxContent.Links.Should().BeEquivalentTo([
            new GetBoxResponseDto.BoxLink
            {
                ExternalId = boxLink.ExternalId.Value,
                AccessCode = boxLink.AccessCode,
                IsEnabled = true,
                Name = boxLink.Name,
                Permissions = new GetBoxResponseDto.Permissions
                {
                    AllowList = true,
                    AllowDownload = true,
                    AllowCreateFolder = true,
                    AllowDeleteFile = true,
                    AllowDeleteFolder = true,
                    AllowMoveItems = true,
                    AllowRenameFile = true,
                    AllowRenameFolder = true,
                    AllowUpload = true,
                },
                WidgetOrigins = []
            }
        ]);
    }
    
    public box_link_tests(HostFixture8081 hostFixture, ITestOutputHelper testOutputHelper) : base(hostFixture, testOutputHelper)
    { 
    }
}