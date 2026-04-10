using FluentAssertions;
using PlikShare.AuditLog;
using PlikShare.AuditLog.Details;
using PlikShare.Boxes.Members.CreateInvitation.Contracts;
using PlikShare.IntegrationTests.Infrastructure;
using Xunit.Abstractions;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.IntegrationTests.TestCases.Boxes;

[Collection(IntegrationTestsCollection.Name)]
public class box_invitation_tests : TestFixture
{
    private AppSignedInUser AppOwner { get; }

    public box_invitation_tests(
        HostFixture8081 hostFixture,
        ITestOutputHelper testOutputHelper) : base(hostFixture, testOutputHelper)
    {
        AppOwner = SignIn(user: Users.AppOwner).Result;
    }

    // --- Functional tests ---

    [Fact]
    public async Task when_box_invitation_is_accepted_member_should_have_accepted_status()
    {
        //given
        var box = await CreateBox(user: AppOwner);
        var invitedUser = await InviteAndRegisterUser(user: AppOwner);

        await Api.Boxes.InviteMember(
            workspaceExternalId: box.WorkspaceExternalId,
            boxExternalId: box.ExternalId,
            request: new CreateBoxInvitationRequestDto(
                MemberEmails: [invitedUser.Email]),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //when
        await Api.BoxExternalAccess.AcceptInvitation(
            boxExternalId: box.ExternalId,
            cookie: invitedUser.Cookie,
            antiforgery: invitedUser.Antiforgery);

        //then
        var boxContent = await Api.Boxes.Get(
            workspaceExternalId: box.WorkspaceExternalId,
            boxExternalId: box.ExternalId,
            cookie: AppOwner.Cookie);

        boxContent.Members.Should().Contain(m =>
            m.MemberEmail == invitedUser.Email &&
            m.WasInvitationAccepted == true);
    }

    [Fact]
    public async Task when_box_invitation_is_rejected_member_should_not_be_visible()
    {
        //given
        var box = await CreateBox(user: AppOwner);
        var invitedUser = await InviteAndRegisterUser(user: AppOwner);

        await Api.Boxes.InviteMember(
            workspaceExternalId: box.WorkspaceExternalId,
            boxExternalId: box.ExternalId,
            request: new CreateBoxInvitationRequestDto(
                MemberEmails: [invitedUser.Email]),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //when
        await Api.BoxExternalAccess.RejectInvitation(
            boxExternalId: box.ExternalId,
            cookie: invitedUser.Cookie,
            antiforgery: invitedUser.Antiforgery);

        //then
        var boxContent = await Api.Boxes.Get(
            workspaceExternalId: box.WorkspaceExternalId,
            boxExternalId: box.ExternalId,
            cookie: AppOwner.Cookie);

        boxContent.Members.Should().NotContain(m =>
            m.MemberEmail == invitedUser.Email);
    }

    [Fact]
    public async Task when_member_leaves_box_they_should_not_be_visible()
    {
        //given
        var box = await CreateBox(user: AppOwner);
        var invitedUser = await InviteAndRegisterUser(user: AppOwner);

        await Api.Boxes.InviteMember(
            workspaceExternalId: box.WorkspaceExternalId,
            boxExternalId: box.ExternalId,
            request: new CreateBoxInvitationRequestDto(
                MemberEmails: [invitedUser.Email]),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        await Api.BoxExternalAccess.AcceptInvitation(
            boxExternalId: box.ExternalId,
            cookie: invitedUser.Cookie,
            antiforgery: invitedUser.Antiforgery);

        //when
        await Api.BoxExternalAccess.LeaveBox(
            boxExternalId: box.ExternalId,
            cookie: invitedUser.Cookie,
            antiforgery: invitedUser.Antiforgery);

        //then
        var boxContent = await Api.Boxes.Get(
            workspaceExternalId: box.WorkspaceExternalId,
            boxExternalId: box.ExternalId,
            cookie: AppOwner.Cookie);

        boxContent.Members.Should().NotContain(m =>
            m.MemberEmail == invitedUser.Email);
    }

    // --- Audit log tests ---

    [Fact]
    public async Task accepting_box_invitation_should_produce_audit_log_entry()
    {
        //given
        var box = await CreateBox(user: AppOwner);
        var invitedUser = await InviteAndRegisterUser(user: AppOwner);

        await Api.Boxes.InviteMember(
            workspaceExternalId: box.WorkspaceExternalId,
            boxExternalId: box.ExternalId,
            request: new CreateBoxInvitationRequestDto(
                MemberEmails: [invitedUser.Email]),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //when
        await Api.BoxExternalAccess.AcceptInvitation(
            boxExternalId: box.ExternalId,
            cookie: invitedUser.Cookie,
            antiforgery: invitedUser.Antiforgery);

        //then
        await AssertAuditLogContains<Audit.Box.InvitationAccepted>(
            expectedEventType: AuditLogEventTypes.Box.InvitationAccepted,
            assertDetails: details =>
            {
                details.Box.ExternalId.Should().Be(box.ExternalId);
            },
            expectedActorEmail: invitedUser.Email,
            expectedSeverity: AuditLogSeverities.Info);
    }

    [Fact]
    public async Task rejecting_box_invitation_should_produce_audit_log_entry()
    {
        //given
        var box = await CreateBox(user: AppOwner);
        var invitedUser = await InviteAndRegisterUser(user: AppOwner);

        await Api.Boxes.InviteMember(
            workspaceExternalId: box.WorkspaceExternalId,
            boxExternalId: box.ExternalId,
            request: new CreateBoxInvitationRequestDto(
                MemberEmails: [invitedUser.Email]),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //when
        await Api.BoxExternalAccess.RejectInvitation(
            boxExternalId: box.ExternalId,
            cookie: invitedUser.Cookie,
            antiforgery: invitedUser.Antiforgery);

        //then
        await AssertAuditLogContains<Audit.Box.InvitationRejected>(
            expectedEventType: AuditLogEventTypes.Box.InvitationRejected,
            assertDetails: details =>
            {
                details.Box.ExternalId.Should().Be(box.ExternalId);
            },
            expectedActorEmail: invitedUser.Email,
            expectedSeverity: AuditLogSeverities.Info);
    }

    [Fact]
    public async Task leaving_box_should_produce_audit_log_entry()
    {
        //given
        var box = await CreateBox(user: AppOwner);
        var invitedUser = await InviteAndRegisterUser(user: AppOwner);

        await Api.Boxes.InviteMember(
            workspaceExternalId: box.WorkspaceExternalId,
            boxExternalId: box.ExternalId,
            request: new CreateBoxInvitationRequestDto(
                MemberEmails: [invitedUser.Email]),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        await Api.BoxExternalAccess.AcceptInvitation(
            boxExternalId: box.ExternalId,
            cookie: invitedUser.Cookie,
            antiforgery: invitedUser.Antiforgery);

        //when
        await Api.BoxExternalAccess.LeaveBox(
            boxExternalId: box.ExternalId,
            cookie: invitedUser.Cookie,
            antiforgery: invitedUser.Antiforgery);

        //then
        await AssertAuditLogContains<Audit.Box.MemberLeft>(
            expectedEventType: AuditLogEventTypes.Box.MemberLeft,
            assertDetails: details =>
            {
                details.Box.ExternalId.Should().Be(box.ExternalId);
            },
            expectedActorEmail: invitedUser.Email,
            expectedSeverity: AuditLogSeverities.Info);
    }
}
