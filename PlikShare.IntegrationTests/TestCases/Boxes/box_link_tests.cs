using FluentAssertions;
using PlikShare.AuditLog;
using PlikShare.AuditLog.Details;
using PlikShare.Boxes.CreateLink.Contracts;
using PlikShare.Boxes.Get.Contracts;
using PlikShare.Boxes.Permissions;
using PlikShare.BoxLinks.UpdatePermissions.Contracts;
using PlikShare.IntegrationTests.Infrastructure;
using Xunit.Abstractions;
using Audit = PlikShare.AuditLog.Details.Audit;

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
    
    // --- Audit log tests ---

    [Fact]
    public async Task creating_box_link_should_produce_audit_log_entry()
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
                Name: "audit-link"),
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        //then
        await AssertAuditLogContains<Audit.Box.LinkCreated>(
            expectedEventType: AuditLogEventTypes.Box.LinkCreated,
            assertDetails: details =>
            {
                details.Workspace.ExternalId.Should().Be(box.WorkspaceExternalId);
                details.Box.ExternalId.Should().Be(box.ExternalId);
                details.LinkExternalId.Should().Be(boxLink.ExternalId);
                details.LinkName.Should().Be("audit-link");
            },
            expectedActorEmail: user.Email,
            expectedSeverity: AuditLogSeverities.Info);
    }

    [Fact]
    public async Task updating_box_link_permissions_should_produce_audit_log_entry()
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
                AllowDeleteFile: false,
                AllowDeleteFolder: false,
                AllowMoveItems: false,
                AllowRenameFile: false,
                AllowRenameFolder: false,
                AllowDownload: true),
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        //then
        await AssertAuditLogContains<Audit.BoxLink.PermissionsUpdated>(
            expectedEventType: AuditLogEventTypes.BoxLink.PermissionsUpdated,
            assertDetails: details =>
            {
                details.Workspace.ExternalId.Should().Be(boxLink.WorkspaceExternalId);
                details.Box.ExternalId.Should().Be(boxLink.BoxExternalId);
                details.BoxLink.ExternalId.Should().Be(boxLink.ExternalId);
                details.Permissions.Should().BeEquivalentTo(new BoxPermissions(
                    AllowDownload: true,
                    AllowUpload: true,
                    AllowList: true,
                    AllowDeleteFile: false,
                    AllowRenameFile: false,
                    AllowMoveItems: false,
                    AllowCreateFolder: true,
                    AllowRenameFolder: false,
                    AllowDeleteFolder: false));
            },
            expectedActorEmail: user.Email,
            expectedSeverity: AuditLogSeverities.Warning);
    }

    public box_link_tests(HostFixture8081 hostFixture, ITestOutputHelper testOutputHelper) : base(hostFixture, testOutputHelper)
    {
    }
}