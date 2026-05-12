using FluentAssertions;
using Microsoft.AspNetCore.Http;
using PlikShare.AuditLog;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.Apis;
using PlikShare.Storages.Encryption;
using PlikShare.Workspaces.ChangeOwner.Contracts;
using PlikShare.Workspaces.Members.AdminAdd.Contracts;
using PlikShare.Workspaces.Members.CreateInvitation.Contracts;
using PlikShare.Workspaces.UpdateMaxTeamMembers.Contracts;
using Xunit.Abstractions;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.IntegrationTests.TestCases.Workspaces;

[Collection(IntegrationTestsCollection.Name)]
public class admin_assign_workspace_member_tests : TestFixture
{
    public admin_assign_workspace_member_tests(
        HostFixture8081 hostFixture,
        ITestOutputHelper testOutputHelper) : base(hostFixture, testOutputHelper)
    {
        hostFixture.ResetUserEncryption().AsTask().Wait();
    }

    private async Task<AppSignedInUser> SignInWithEncryption(User user)
    {
        var signedIn = await SignIn(user);
        var setup = await Api.UserEncryptionPassword.Setup(
            userExternalId: signedIn.ExternalId,
            encryptionPassword: DefaultTestEncryptionPassword,
            cookie: signedIn.Cookie,
            antiforgery: signedIn.Antiforgery);

        return signedIn with { EncryptionCookie = setup.EncryptionCookie };
    }

    // --- happy paths: direct assignment (accepted=TRUE, no email) ---

    [Fact]
    public async Task admin_assign_marks_membership_as_accepted_and_sends_no_invitation_email()
    {
        // The whole point of admin-assign vs owner-invite: skip the invitation handshake.
        // The user lands in the workspace immediately, no pending invitation, no email.
        //given
        var appOwner = await SignIn(Users.AppOwner);

        var storage = await CreateHardDriveStorage(
            user: appOwner,
            encryptionType: StorageEncryptionType.None);

        var workspace = await CreateWorkspace(
            storage: storage,
            user: appOwner);

        var emailProvider = await CreateAndActivateEmailProviderIfMissing(user: appOwner);
        emailProvider.Should().NotBeNull();

        var target = await InviteAndRegisterUser(appOwner);

        // Snapshot the email count addressed to this target AFTER registration but BEFORE
        // assignment. The registration step itself sends a user-invitation email, so we
        // need a baseline rather than asserting against zero.
        var emailsToTargetBefore = CountEmailsTo(target.Email);

        //when
        await Api.WorkspacesAdmin.AssignMember(
            externalId: workspace.ExternalId,
            request: new AdminAddWorkspaceMemberRequestDto
            {
                MemberExternalId = target.ExternalId,
                AllowShare = false
            },
            cookie: appOwner.Cookie,
            antiforgery: appOwner.Antiforgery);

        //then — membership row exists AND is auto-accepted (admin doesn't ask the user).
        var details = await Api.Users.GetDetails(
            userExternalId: target.ExternalId,
            cookie: appOwner.Cookie);

        details.SharedWorkspaces
            .Should().ContainSingle(w => w.ExternalId == workspace.ExternalId)
            .Which.WasInvitationAccepted.Should().BeTrue();

        // No new email enqueued for the target between snapshot and now.
        await Task.Delay(100);
        CountEmailsTo(target.Email).Should().Be(emailsToTargetBefore,
            "admin-assign must not send any email — the user is not being invited, just attached");
    }

    private int CountEmailsTo(string email)
    {
        return ResendEmailServer
            .ReceivedEmails
            .Count(r => r.Body.To.Contains(email, StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task admin_with_sek_can_assign_target_with_encryption_to_full_encrypted_workspace_and_auto_grant_wek()
    {
        //given — first owner creates the storage, second owner has encryption set up
        // BEFORE storage creation so they automatically pick up a sek wrap (storage-admin
        // path). Target has encryption set up → falls into the auto-grant branch.
        var firstOwner = await SignInWithEncryption(Users.AppOwner);
        var secondOwner = await SignInWithEncryption(Users.SecondAppOwner);

        var storage = await CreateHardDriveStorage(
            user: firstOwner,
            encryptionType: StorageEncryptionType.Full);

        var workspace = await CreateWorkspace(
            storage: storage,
            user: firstOwner);

        GetStorageEncryptionKeyOwnerEmails(storage.ExternalId)
            .Should().Contain(secondOwner.Email);

        var target = await InviteAndRegisterUser(firstOwner);
        var targetSetup = await Api.UserEncryptionPassword.Setup(
            userExternalId: target.ExternalId,
            encryptionPassword: DefaultTestEncryptionPassword,
            cookie: target.Cookie,
            antiforgery: target.Antiforgery);
        targetSetup.Should().NotBeNull();

        HasWorkspaceEncryptionKey(workspace.ExternalId, target.Email).Should().BeFalse(
            "target has no wek before assignment");

        //when — secondOwner (admin via app-owner role) assigns target.
        await Api.WorkspacesAdmin.AssignMember(
            externalId: workspace.ExternalId,
            request: new AdminAddWorkspaceMemberRequestDto
            {
                MemberExternalId = target.ExternalId,
                AllowShare = false
            },
            cookie: secondOwner.Cookie,
            antiforgery: secondOwner.Antiforgery,
            userEncryptionSession: secondOwner.EncryptionCookie);

        //then
        HasWorkspaceEncryptionKey(workspace.ExternalId, target.Email).Should().BeTrue(
            "auto-grant must provision a wek wrap for the target sealed by the admin's key path");

        var details = await Api.Users.GetDetails(
            userExternalId: target.ExternalId,
            cookie: firstOwner.Cookie);

        details.SharedWorkspaces
            .Should().ContainSingle(w => w.ExternalId == workspace.ExternalId)
            .Which.WasInvitationAccepted.Should().BeTrue();
    }

    [Fact]
    public async Task admin_assigning_registered_user_without_encryption_to_full_workspace_inserts_row_without_wek()
    {
        // Deferred-grant path: target has no public key, so no wek can be wrapped.
        // Membership row goes in as accepted; owner will be notified later when the
        // target sets up their encryption password (NotifyOwnersOfPendingGrantsQuery).
        //given
        var owner = await SignInWithEncryption(Users.AppOwner);

        var storage = await CreateHardDriveStorage(
            user: owner,
            encryptionType: StorageEncryptionType.Full);

        var workspace = await CreateWorkspace(
            storage: storage,
            user: owner);

        var target = await InviteAndRegisterUser(owner);

        //when
        await Api.WorkspacesAdmin.AssignMember(
            externalId: workspace.ExternalId,
            request: new AdminAddWorkspaceMemberRequestDto
            {
                MemberExternalId = target.ExternalId,
                AllowShare = false
            },
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery,
            userEncryptionSession: owner.EncryptionCookie);

        //then
        HasWorkspaceEncryptionKey(workspace.ExternalId, target.Email).Should().BeFalse(
            "target has no public key, so the auto-grant branch cannot wrap a wek");

        HasEphemeralWorkspaceEncryptionKey(workspace.ExternalId, target.Email).Should().BeFalse(
            "admin-assign never stages an ephemeral DEK — that path belongs to the invitation flow");

        var details = await Api.Users.GetDetails(
            userExternalId: target.ExternalId,
            cookie: owner.Cookie);

        details.SharedWorkspaces
            .Should().ContainSingle(w => w.ExternalId == workspace.ExternalId)
            .Which.WasInvitationAccepted.Should().BeTrue();
    }

    // --- error paths ---

    [Fact]
    public async Task assign_invitation_only_user_returns_user_not_registered()
    {
        //given — user invited but never registered (no password set yet).
        var owner = await SignIn(Users.AppOwner);

        var storage = await CreateHardDriveStorage(
            user: owner,
            encryptionType: StorageEncryptionType.None);

        var workspace = await CreateWorkspace(
            storage: storage,
            user: owner);

        var invited = await InviteUser(owner);

        //when
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.WorkspacesAdmin.AssignMember(
                externalId: workspace.ExternalId,
                request: new AdminAddWorkspaceMemberRequestDto
                {
                    MemberExternalId = invited.ExternalId,
                    AllowShare = false
                },
                cookie: owner.Cookie,
                antiforgery: owner.Antiforgery));

        //then
        apiError.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        apiError.HttpError.Should().NotBeNull();
        apiError.HttpError!.Code.Should().Be("user-not-registered");
    }

    [Fact]
    public async Task assign_without_actor_encryption_session_on_full_workspace_returns_user_encryption_session_required()
    {
        //given
        var owner = await SignInWithEncryption(Users.AppOwner);

        var storage = await CreateHardDriveStorage(
            user: owner,
            encryptionType: StorageEncryptionType.Full);

        var workspace = await CreateWorkspace(
            storage: storage,
            user: owner);

        var target = await InviteAndRegisterUser(owner);

        //when — owner omits their encryption cookie. Server cannot re-wrap the DEK.
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.WorkspacesAdmin.AssignMember(
                externalId: workspace.ExternalId,
                request: new AdminAddWorkspaceMemberRequestDto
                {
                    MemberExternalId = target.ExternalId,
                    AllowShare = false
                },
                cookie: owner.Cookie,
                antiforgery: owner.Antiforgery));

        //then
        apiError.StatusCode.Should().Be(StatusCodes.Status423Locked);
        apiError.HttpError.Should().NotBeNull();
        apiError.HttpError!.Code.Should().Be("user-encryption-session-required");
    }

    [Fact]
    public async Task assign_by_admin_without_sek_or_wek_returns_not_a_storage_admin()
    {
        //given — second owner sets up encryption AFTER storage creation, so they hold
        // neither a sek wrap nor a wek wrap. Even though they pass the admin role
        // check, they cannot decrypt the workspace DEK.
        var firstOwner = await SignInWithEncryption(Users.AppOwner);

        var storage = await CreateHardDriveStorage(
            user: firstOwner,
            encryptionType: StorageEncryptionType.Full);

        var secondOwner = await SignInWithEncryption(Users.SecondAppOwner);

        var workspace = await CreateWorkspace(
            storage: storage,
            user: firstOwner);

        GetStorageEncryptionKeyOwnerEmails(storage.ExternalId)
            .Should().NotContain(secondOwner.Email);

        var target = await InviteAndRegisterUser(firstOwner);

        //when
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.WorkspacesAdmin.AssignMember(
                externalId: workspace.ExternalId,
                request: new AdminAddWorkspaceMemberRequestDto
                {
                    MemberExternalId = target.ExternalId,
                    AllowShare = false
                },
                cookie: secondOwner.Cookie,
                antiforgery: secondOwner.Antiforgery,
                userEncryptionSession: secondOwner.EncryptionCookie));

        //then
        apiError.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        apiError.HttpError.Should().NotBeNull();
        apiError.HttpError!.Code.Should().Be("not-a-storage-admin");
    }

    [Fact]
    public async Task assign_when_target_is_already_the_workspace_owner_returns_member_already_assigned()
    {
        //given
        var owner = await SignIn(Users.AppOwner);

        var storage = await CreateHardDriveStorage(
            user: owner,
            encryptionType: StorageEncryptionType.None);

        var workspace = await CreateWorkspace(
            storage: storage,
            user: owner);

        //when — owner tries to assign themselves to their own workspace.
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.WorkspacesAdmin.AssignMember(
                externalId: workspace.ExternalId,
                request: new AdminAddWorkspaceMemberRequestDto
                {
                    MemberExternalId = owner.ExternalId,
                    AllowShare = false
                },
                cookie: owner.Cookie,
                antiforgery: owner.Antiforgery));

        //then
        apiError.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        apiError.HttpError.Should().NotBeNull();
        apiError.HttpError!.Code.Should().Be("workspace-member-already-assigned");
    }

    [Fact]
    public async Task assign_when_target_is_already_a_member_returns_member_already_assigned()
    {
        //given
        var owner = await SignIn(Users.AppOwner);

        var storage = await CreateHardDriveStorage(
            user: owner,
            encryptionType: StorageEncryptionType.None);

        var workspace = await CreateWorkspace(
            storage: storage,
            user: owner);

        var target = await InviteAndRegisterUser(owner);

        // First assignment succeeds.
        await Api.WorkspacesAdmin.AssignMember(
            externalId: workspace.ExternalId,
            request: new AdminAddWorkspaceMemberRequestDto
            {
                MemberExternalId = target.ExternalId,
                AllowShare = false
            },
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        //when — second assignment of the same target.
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.WorkspacesAdmin.AssignMember(
                externalId: workspace.ExternalId,
                request: new AdminAddWorkspaceMemberRequestDto
                {
                    MemberExternalId = target.ExternalId,
                    AllowShare = false
                },
                cookie: owner.Cookie,
                antiforgery: owner.Antiforgery));

        //then
        apiError.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        apiError.HttpError.Should().NotBeNull();
        apiError.HttpError!.Code.Should().Be("workspace-member-already-assigned");
    }

    [Fact]
    public async Task assign_exceeding_max_team_members_returns_max_team_members_exceeded()
    {
        //given
        var owner = await SignIn(Users.AppOwner);

        var storage = await CreateHardDriveStorage(
            user: owner,
            encryptionType: StorageEncryptionType.None);

        var workspace = await CreateWorkspace(
            storage: storage,
            user: owner);

        await Api.WorkspacesAdmin.UpdateMaxTeamMembers(
            externalId: workspace.ExternalId,
            request: new UpdateWorkspaceMaxTeamMembersRequestDto
            {
                MaxTeamMembers = 0
            },
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        var target = await InviteAndRegisterUser(owner);

        //when
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.WorkspacesAdmin.AssignMember(
                externalId: workspace.ExternalId,
                request: new AdminAddWorkspaceMemberRequestDto
                {
                    MemberExternalId = target.ExternalId,
                    AllowShare = false
                },
                cookie: owner.Cookie,
                antiforgery: owner.Antiforgery));

        //then
        apiError.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        apiError.HttpError.Should().NotBeNull();
        apiError.HttpError!.Code.Should().Be("max-team-members-exceeded");
    }

    [Fact]
    public async Task non_admin_user_cannot_assign_workspace_member()
    {
        //given
        var owner = await SignIn(Users.AppOwner);

        var storage = await CreateHardDriveStorage(
            user: owner,
            encryptionType: StorageEncryptionType.None);

        var workspace = await CreateWorkspace(
            storage: storage,
            user: owner);

        var plainUser = await InviteAndRegisterUser(owner);
        var target = await InviteAndRegisterUser(owner);

        //when
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.WorkspacesAdmin.AssignMember(
                externalId: workspace.ExternalId,
                request: new AdminAddWorkspaceMemberRequestDto
                {
                    MemberExternalId = target.ExternalId,
                    AllowShare = false
                },
                cookie: plainUser.Cookie,
                antiforgery: plainUser.Antiforgery));

        //then
        apiError.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    // --- audit log ---

    [Fact]
    public async Task admin_assigning_member_produces_audit_log_entry()
    {
        //given
        var owner = await SignIn(Users.AppOwner);

        var storage = await CreateHardDriveStorage(
            user: owner,
            encryptionType: StorageEncryptionType.None);

        var workspace = await CreateWorkspace(
            storage: storage,
            user: owner);

        var target = await InviteAndRegisterUser(owner);

        //when
        await Api.WorkspacesAdmin.AssignMember(
            externalId: workspace.ExternalId,
            request: new AdminAddWorkspaceMemberRequestDto
            {
                MemberExternalId = target.ExternalId,
                AllowShare = true
            },
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        //then
        await AssertAuditLogContains<Audit.Workspace.AdminAssignedMember>(
            expectedEventType: AuditLogEventTypes.Workspace.AdminAssignedMember,
            assertDetails: details =>
            {
                details.Workspace.ExternalId.Should().Be(workspace.ExternalId);
                details.Member.ExternalId.Should().Be(target.ExternalId);
                details.Member.Email.Should().Be(target.Email);
                details.AllowShare.Should().BeTrue();
            },
            expectedActorEmail: owner.Email,
            expectedSeverity: AuditLogSeverities.Warning);
    }

    // --- list endpoint ---

    [Fact]
    public async Task list_all_workspaces_returns_every_workspace_in_the_system()
    {
        //given
        var owner = await SignIn(Users.AppOwner);

        var storage = await CreateHardDriveStorage(
            user: owner,
            encryptionType: StorageEncryptionType.None);

        var workspaceA = await CreateWorkspace(storage: storage, user: owner);
        var workspaceB = await CreateWorkspace(storage: storage, user: owner);

        //when
        var response = await Api.WorkspacesAdmin.ListAll(cookie: owner.Cookie);

        //then
        response.Items.Should().Contain(w => w.ExternalId == workspaceA.ExternalId);
        response.Items.Should().Contain(w => w.ExternalId == workspaceB.ExternalId);

        var rowA = response.Items.Single(w => w.ExternalId == workspaceA.ExternalId);
        rowA.Owner.Email.Should().Be(owner.Email);
        rowA.Owner.ExternalId.Should().Be(owner.ExternalId);
        rowA.Storage.ExternalId.Should().Be(storage.ExternalId);
        rowA.Storage.EncryptionType.Should().Be("none");
    }

    [Fact]
    public async Task list_all_workspaces_excludes_workspaces_user_already_owns_or_is_member_of()
    {
        //given
        var owner = await SignIn(Users.AppOwner);

        var storage = await CreateHardDriveStorage(
            user: owner,
            encryptionType: StorageEncryptionType.None);

        var ownedByOwner = await CreateWorkspace(storage: storage, user: owner);
        var notInvolved = await CreateWorkspace(storage: storage, user: owner);

        var target = await InviteAndRegisterUser(owner);

        var thirdWorkspace = await CreateWorkspace(storage: storage, user: owner);
        await Api.WorkspacesAdmin.AssignMember(
            externalId: thirdWorkspace.ExternalId,
            request: new AdminAddWorkspaceMemberRequestDto
            {
                MemberExternalId = target.ExternalId,
                AllowShare = false
            },
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        var willBeOwnedByTarget = await CreateWorkspace(storage: storage, user: owner);
        await Api.WorkspacesAdmin.UpdateOwner(
            externalId: willBeOwnedByTarget.ExternalId,
            request: new ChangeWorkspaceOwnerRequestDto(NewOwnerExternalId: target.ExternalId),
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        //when
        var response = await Api.WorkspacesAdmin.ListAll(
            cookie: owner.Cookie,
            excludeMemberOrOwner: target.ExternalId);

        //then
        response.Items.Should().Contain(w => w.ExternalId == ownedByOwner.ExternalId);
        response.Items.Should().Contain(w => w.ExternalId == notInvolved.ExternalId);

        response.Items.Should().NotContain(w => w.ExternalId == thirdWorkspace.ExternalId,
            "target is already a member of this workspace");
        response.Items.Should().NotContain(w => w.ExternalId == willBeOwnedByTarget.ExternalId,
            "target is now the owner of this workspace");
    }

    [Fact]
    public async Task non_admin_user_cannot_list_all_workspaces()
    {
        //given
        var owner = await SignIn(Users.AppOwner);
        var plainUser = await InviteAndRegisterUser(owner);

        //when
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.WorkspacesAdmin.ListAll(cookie: plainUser.Cookie));

        //then
        apiError.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    // --- transfer-ownership flow (admin assigns workspace as owner, not member) ---

    [Fact]
    public async Task admin_transfer_ownership_moves_workspace_to_target_owned_list_and_off_source()
    {
        // Covers the user-details "+ assign workspace" button in the "Workspaces owned by
        // User" section: admin picks an existing workspace and transfers ownership to the
        // target. After the call, GetUserDetails reports the workspace under target's
        // Workspaces (owned) and under nobody else's.
        //given
        var firstOwner = await SignIn(Users.AppOwner);
        var target = await InviteAndRegisterUser(firstOwner);

        var storage = await CreateHardDriveStorage(
            user: firstOwner,
            encryptionType: StorageEncryptionType.None);

        var workspace = await CreateWorkspace(
            storage: storage,
            user: firstOwner);

        // Sanity: workspace starts owned by firstOwner.
        var firstOwnerBefore = await Api.Users.GetDetails(
            userExternalId: firstOwner.ExternalId,
            cookie: firstOwner.Cookie);

        firstOwnerBefore.Workspaces
            .Should().Contain(w => w.ExternalId == workspace.ExternalId);

        //when
        await Api.WorkspacesAdmin.UpdateOwner(
            externalId: workspace.ExternalId,
            request: new ChangeWorkspaceOwnerRequestDto(NewOwnerExternalId: target.ExternalId),
            cookie: firstOwner.Cookie,
            antiforgery: firstOwner.Antiforgery);

        //then — workspace appears in target's owned list…
        var targetDetails = await Api.Users.GetDetails(
            userExternalId: target.ExternalId,
            cookie: firstOwner.Cookie);

        targetDetails.Workspaces
            .Should().ContainSingle(w => w.ExternalId == workspace.ExternalId);

        targetDetails.SharedWorkspaces
            .Should().NotContain(w => w.ExternalId == workspace.ExternalId,
                "transfer to owner must not leave a stale membership row");

        //…and disappears from the source's owned list.
        var firstOwnerAfter = await Api.Users.GetDetails(
            userExternalId: firstOwner.ExternalId,
            cookie: firstOwner.Cookie);

        firstOwnerAfter.Workspaces
            .Should().NotContain(w => w.ExternalId == workspace.ExternalId);
    }

    [Fact]
    public async Task admin_transfer_ownership_to_existing_member_promotes_them_and_removes_membership_row()
    {
        // Edge case: target is already a member of the workspace. The transfer should
        // promote them to owner AND remove the wm row so they don't appear in two lists.
        //given
        var owner = await SignIn(Users.AppOwner);
        var target = await InviteAndRegisterUser(owner);

        var storage = await CreateHardDriveStorage(
            user: owner,
            encryptionType: StorageEncryptionType.None);

        var workspace = await CreateWorkspace(
            storage: storage,
            user: owner);

        // First make target a member.
        await Api.WorkspacesAdmin.AssignMember(
            externalId: workspace.ExternalId,
            request: new AdminAddWorkspaceMemberRequestDto
            {
                MemberExternalId = target.ExternalId,
                AllowShare = false
            },
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        var beforeTransfer = await Api.Users.GetDetails(
            userExternalId: target.ExternalId,
            cookie: owner.Cookie);

        beforeTransfer.SharedWorkspaces
            .Should().ContainSingle(w => w.ExternalId == workspace.ExternalId);
        beforeTransfer.Workspaces
            .Should().NotContain(w => w.ExternalId == workspace.ExternalId);

        //when
        await Api.WorkspacesAdmin.UpdateOwner(
            externalId: workspace.ExternalId,
            request: new ChangeWorkspaceOwnerRequestDto(NewOwnerExternalId: target.ExternalId),
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        //then
        var afterTransfer = await Api.Users.GetDetails(
            userExternalId: target.ExternalId,
            cookie: owner.Cookie);

        afterTransfer.Workspaces
            .Should().ContainSingle(w => w.ExternalId == workspace.ExternalId,
                "target was promoted from member to owner");

        afterTransfer.SharedWorkspaces
            .Should().NotContain(w => w.ExternalId == workspace.ExternalId,
                "the previous membership row must be removed on owner promotion");
    }

    [Fact]
    public async Task admin_transfer_ownership_to_user_without_encryption_on_full_workspace_returns_error()
    {
        // The picker UI lets admin pick any workspace; for full-encryption workspaces the
        // backend rejects transfers to users without encryption configured. This regression
        // guards the error surface that the picker relies on.
        //given
        var owner = await SignInWithEncryption(Users.AppOwner);

        var storage = await CreateHardDriveStorage(
            user: owner,
            encryptionType: StorageEncryptionType.Full);

        var workspace = await CreateWorkspace(
            storage: storage,
            user: owner);

        // Target: registered but no encryption setup.
        var target = await InviteAndRegisterUser(owner);

        //when
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.WorkspacesAdmin.UpdateOwner(
                externalId: workspace.ExternalId,
                request: new ChangeWorkspaceOwnerRequestDto(NewOwnerExternalId: target.ExternalId),
                cookie: owner.Cookie,
                antiforgery: owner.Antiforgery,
                userEncryptionSession: owner.EncryptionCookie));

        //then
        apiError.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        apiError.HttpError.Should().NotBeNull();
        apiError.HttpError!.Code.Should().Be("member-encryption-not-set-up");
    }

    // --- sanity: existing owner-invite path is unaffected ---

    [Fact]
    public async Task existing_owner_invite_path_still_works_independently_of_admin_assign()
    {
        //given
        var owner = await SignIn(Users.AppOwner);

        var storage = await CreateHardDriveStorage(
            user: owner,
            encryptionType: StorageEncryptionType.None);

        var workspace = await CreateWorkspace(
            storage: storage,
            user: owner);

        var member = await InviteAndRegisterUser(owner);

        //when
        var response = await Api.Workspaces.InviteMember(
            externalId: workspace.ExternalId,
            request: new CreateWorkspaceMemberInvitationRequestDto(
                MemberEmails: [member.Email],
                AllowShare: false,
                EphemeralDekLifetimeHours: null),
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        //then
        response.Members.Should().ContainSingle(m => m.ExternalId == member.ExternalId);

        // Owner-invite path still inserts as not-accepted and enqueues the invitation email.
        var details = await Api.Users.GetDetails(
            userExternalId: member.ExternalId,
            cookie: owner.Cookie);

        details.SharedWorkspaces
            .Should().ContainSingle(w => w.ExternalId == workspace.ExternalId)
            .Which.WasInvitationAccepted.Should().BeFalse();
    }

}
