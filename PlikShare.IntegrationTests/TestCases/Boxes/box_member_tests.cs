using FluentAssertions;
using PlikShare.AuditLog;
using PlikShare.AuditLog.Details;
using PlikShare.Boxes.Get.Contracts;
using PlikShare.Boxes.Members.CreateInvitation.Contracts;
using PlikShare.Boxes.Members.UpdatePermissions.Contracts;
using PlikShare.Boxes.Permissions;
using PlikShare.IntegrationTests.Infrastructure;
using Xunit.Abstractions;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.IntegrationTests.TestCases.Boxes;

[Collection(IntegrationTestsCollection.Name)]
public class box_member_tests : TestFixture
{
    private AppSignedInUser AppOwner { get; }

    public box_member_tests(
        HostFixture8081 hostFixture,
        ITestOutputHelper testOutputHelper) : base(hostFixture, testOutputHelper)
    {
        AppOwner = SignIn(user: Users.AppOwner).Result;
    }

    // --- Functional tests ---

    [Fact]
    public async Task when_member_is_invited_to_box_it_should_be_visible_in_box_details()
    {
        //given
        var box = await CreateBox(user: AppOwner);
        var invitedUser = await InviteAndRegisterUser(user: AppOwner);

        //when
        await Api.Boxes.InviteMember(
            workspaceExternalId: box.WorkspaceExternalId,
            boxExternalId: box.ExternalId,
            request: new CreateBoxInvitationRequestDto(
                MemberEmails: [invitedUser.Email]),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var boxContent = await Api.Boxes.Get(
            workspaceExternalId: box.WorkspaceExternalId,
            boxExternalId: box.ExternalId,
            cookie: AppOwner.Cookie);

        boxContent.Members.Should().Contain(m =>
            m.MemberEmail == invitedUser.Email);
    }

    [Fact]
    public async Task when_member_is_revoked_from_box_it_should_not_be_visible_in_box_details()
    {
        //given
        var box = await CreateBox(user: AppOwner);
        var invitedUser = await InviteAndRegisterUser(user: AppOwner);

        var invitationResponse = await Api.Boxes.InviteMember(
            workspaceExternalId: box.WorkspaceExternalId,
            boxExternalId: box.ExternalId,
            request: new CreateBoxInvitationRequestDto(
                MemberEmails: [invitedUser.Email]),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        var memberExternalId = invitationResponse.Members.First().ExternalId;

        //when
        await Api.Boxes.RevokeMember(
            workspaceExternalId: box.WorkspaceExternalId,
            boxExternalId: box.ExternalId,
            memberExternalId: memberExternalId,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var boxContent = await Api.Boxes.Get(
            workspaceExternalId: box.WorkspaceExternalId,
            boxExternalId: box.ExternalId,
            cookie: AppOwner.Cookie);

        boxContent.Members.Should().NotContain(m =>
            m.MemberEmail == invitedUser.Email);
    }

    [Fact]
    public async Task when_member_permissions_are_updated_it_should_be_reflected_in_box_details()
    {
        //given
        var box = await CreateBox(user: AppOwner);
        var invitedUser = await InviteAndRegisterUser(user: AppOwner);

        var invitationResponse = await Api.Boxes.InviteMember(
            workspaceExternalId: box.WorkspaceExternalId,
            boxExternalId: box.ExternalId,
            request: new CreateBoxInvitationRequestDto(
                MemberEmails: [invitedUser.Email]),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        var memberExternalId = invitationResponse.Members.First().ExternalId;

        //when
        await Api.Boxes.UpdateMemberPermissions(
            workspaceExternalId: box.WorkspaceExternalId,
            boxExternalId: box.ExternalId,
            memberExternalId: memberExternalId,
            request: new UpdateBoxMemberPermissionsRequestDto
            {
                AllowDownload = true,
                AllowUpload = true,
                AllowList = true,
                AllowDeleteFile = false,
                AllowRenameFile = false,
                AllowMoveItems = false,
                AllowCreateFolder = true,
                AllowRenameFolder = false,
                AllowDeleteFolder = false
            },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var boxContent = await Api.Boxes.Get(
            workspaceExternalId: box.WorkspaceExternalId,
            boxExternalId: box.ExternalId,
            cookie: AppOwner.Cookie);

        var member = boxContent.Members.Should().Contain(m =>
            m.MemberEmail == invitedUser.Email).Which;

        member.Permissions.Should().BeEquivalentTo(new GetBoxResponseDto.Permissions
        {
            AllowDownload = true,
            AllowUpload = true,
            AllowList = true,
            AllowDeleteFile = false,
            AllowRenameFile = false,
            AllowMoveItems = false,
            AllowCreateFolder = true,
            AllowRenameFolder = false,
            AllowDeleteFolder = false
        });
    }

    // --- Audit log tests ---

    [Fact]
    public async Task inviting_member_to_box_should_produce_audit_log_entry()
    {
        //given
        var box = await CreateBox(user: AppOwner);
        var invitedUser = await InviteAndRegisterUser(user: AppOwner);

        //when
        await Api.Boxes.InviteMember(
            workspaceExternalId: box.WorkspaceExternalId,
            boxExternalId: box.ExternalId,
            request: new CreateBoxInvitationRequestDto(
                MemberEmails: [invitedUser.Email]),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<Audit.Box.MemberInvited>(
            expectedEventType: AuditLogEventTypes.Box.MemberInvited,
            assertDetails: details =>
            {
                details.Workspace.ExternalId.Should().Be(box.WorkspaceExternalId);
                details.Box.ExternalId.Should().Be(box.ExternalId);
                details.Members.Should().Contain(m => m.Email == invitedUser.Email);
            },
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Info);
    }

    [Fact]
    public async Task revoking_member_from_box_should_produce_audit_log_entry()
    {
        //given
        var box = await CreateBox(user: AppOwner);
        var invitedUser = await InviteAndRegisterUser(user: AppOwner);

        var invitationResponse = await Api.Boxes.InviteMember(
            workspaceExternalId: box.WorkspaceExternalId,
            boxExternalId: box.ExternalId,
            request: new CreateBoxInvitationRequestDto(
                MemberEmails: [invitedUser.Email]),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        var memberExternalId = invitationResponse.Members.First().ExternalId;

        //when
        await Api.Boxes.RevokeMember(
            workspaceExternalId: box.WorkspaceExternalId,
            boxExternalId: box.ExternalId,
            memberExternalId: memberExternalId,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<Audit.Box.MemberRevoked>(
            expectedEventType: AuditLogEventTypes.Box.MemberRevoked,
            assertDetails: details =>
            {
                details.Workspace.ExternalId.Should().Be(box.WorkspaceExternalId);
                details.Box.ExternalId.Should().Be(box.ExternalId);
                details.Member.Email.Should().Be(invitedUser.Email);
            },
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Warning);
    }

    [Fact]
    public async Task updating_member_permissions_should_produce_audit_log_entry()
    {
        //given
        var box = await CreateBox(user: AppOwner);
        var invitedUser = await InviteAndRegisterUser(user: AppOwner);

        var invitationResponse = await Api.Boxes.InviteMember(
            workspaceExternalId: box.WorkspaceExternalId,
            boxExternalId: box.ExternalId,
            request: new CreateBoxInvitationRequestDto(
                MemberEmails: [invitedUser.Email]),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        var memberExternalId = invitationResponse.Members.First().ExternalId;

        //when
        await Api.Boxes.UpdateMemberPermissions(
            workspaceExternalId: box.WorkspaceExternalId,
            boxExternalId: box.ExternalId,
            memberExternalId: memberExternalId,
            request: new UpdateBoxMemberPermissionsRequestDto
            {
                AllowDownload = true,
                AllowUpload = true,
                AllowList = true,
                AllowDeleteFile = false,
                AllowRenameFile = false,
                AllowMoveItems = false,
                AllowCreateFolder = true,
                AllowRenameFolder = false,
                AllowDeleteFolder = false
            },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<Audit.Box.MemberPermissionsUpdated>(
            expectedEventType: AuditLogEventTypes.Box.MemberPermissionsUpdated,
            assertDetails: details =>
            {
                details.Workspace.ExternalId.Should().Be(box.WorkspaceExternalId);
                details.Box.ExternalId.Should().Be(box.ExternalId);
                details.Member.Email.Should().Be(invitedUser.Email);
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
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Warning);
    }
}
