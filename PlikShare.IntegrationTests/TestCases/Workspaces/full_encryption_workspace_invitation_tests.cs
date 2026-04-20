using FluentAssertions;
using PlikShare.Auth.Contracts;
using PlikShare.Core.Emails;
using PlikShare.EmailProviders.ExternalProviders.Resend;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.Storages.Encryption;
using PlikShare.Workspaces.Members.CreateInvitation.Contracts;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Workspaces;

[Collection(IntegrationTestsCollection.Name)]
public class full_encryption_workspace_invitation_tests : TestFixture
{
    private AppSignedInUser AppOwner { get; }
    private AppEmailProvider EmailProvider { get; }

    public full_encryption_workspace_invitation_tests(HostFixture8081 hostFixture, ITestOutputHelper testOutputHelper)
        : base(hostFixture, testOutputHelper)
    {
        hostFixture.ResetUserEncryption().AsTask().Wait();
        AppOwner = SignIn(user: Users.AppOwner).Result;
        EmailProvider = CreateAndActivateEmailProviderIfMissing(user: AppOwner).Result;
    }

    [Fact]
    public async Task invitee_with_encryption_set_up_can_download_file_immediately_after_invite_without_manual_grant()
    {
        // The hard proof that auto-grant worked end-to-end: bytes come out decrypted,
        // which is only possible if wek_workspace_encryption_keys has a wrap sealed to
        // the invitee's public key.
        //given
        var storage = await CreateHardDriveStorage(
            user: AppOwner,
            encryptionType: StorageEncryptionType.Full);

        var workspace = await CreateWorkspace(
            storage: storage,
            user: AppOwner);

        var folder = await CreateFolder(
            workspace: workspace,
            user: AppOwner);

        var content = Random.Bytes(256);

        var uploadedFile = await UploadFile(
            content: content,
            fileName: $"{Random.Name("file")}.bin",
            contentType: "application/octet-stream",
            folder: folder,
            workspace: workspace,
            user: AppOwner);

        var invitee = await InviteAndRegisterUser(user: AppOwner);
        var inviteeSetup = await Api.UserEncryptionPassword.Setup(
            userExternalId: invitee.ExternalId,
            encryptionPassword: "Invitee-Pass-1!",
            cookie: invitee.Cookie,
            antiforgery: invitee.Antiforgery);

        //when — owner invites; auto-grant wraps the Workspace DEK for the invitee.
        await Api.Workspaces.InviteMember(
            externalId: workspace.ExternalId,
            request: new CreateWorkspaceMemberInvitationRequestDto(
                MemberEmails: [invitee.Email],
                AllowShare: false),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery,
            userEncryptionSession: storage.WorkspaceEncryptionSession);

        await Api.Workspaces.AcceptInvitation(
            externalId: workspace.ExternalId,
            cookie: invitee.Cookie,
            antiforgery: invitee.Antiforgery);

        //then — invitee can download the file using ONLY their own encryption session.
        var downloaded = await DownloadFile(
            fileExternalId: uploadedFile.ExternalId,
            workspace: workspace with { WorkspaceEncryptionSession = inviteeSetup.EncryptionCookie },
            user: invitee);

        downloaded.Should().Equal(content);
    }

    [Fact]
    public async Task owner_is_emailed_after_invitee_sets_up_encryption_password()
    {
        // Deferred path fires at setup time: no wek_* exists yet, so
        // NotifyOwnersOfPendingGrantsQuery enqueues the grant-required email now
        // that the invitee has a public key.
        //given
        var storage = await CreateHardDriveStorage(
            user: AppOwner,
            encryptionType: StorageEncryptionType.Full);

        var workspace = await CreateWorkspace(
            storage: storage,
            user: AppOwner);

        var invitee = await InviteAndRegisterUser(user: AppOwner);

        await Api.Workspaces.InviteMember(
            externalId: workspace.ExternalId,
            request: new CreateWorkspaceMemberInvitationRequestDto(
                MemberEmails: [invitee.Email],
                AllowShare: false),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery,
            userEncryptionSession: storage.WorkspaceEncryptionSession);

        //when — invitee finally configures encryption.
        await Api.UserEncryptionPassword.Setup(
            userExternalId: invitee.ExternalId,
            encryptionPassword: "Invitee-Pass-1!",
            cookie: invitee.Cookie,
            antiforgery: invitee.Antiforgery);

        //then — owner now receives the grant-required email.
        var (expectedTitle, expectedContent) = Emails.WorkspaceEncryptionKeyGrantRequired(
            applicationName: AppSettings.ApplicationName.Name,
            inviteeEmail: invitee.Email,
            workspaceName: workspace.Name);

        var expectedHtml = EmailTemplates.Generic.Build(
            title: expectedTitle,
            content: expectedContent);

        await WaitFor(() =>
        {
            ResendEmailServer.ShouldContainEmails([
                new ResendRequestBody(
                    From: EmailProvider.EmailFrom,
                    To: [AppOwner.Email],
                    Subject: expectedTitle,
                    Html: expectedHtml)
            ]);
        });
    }

    [Fact]
    public async Task invitee_is_emailed_when_owner_manually_grants_encryption_access_in_the_deferred_flow()
    {
        // Deferred flow: invitee was invited before setting up encryption. After they
        // complete setup the owner must manually grant. That manual grant DOES notify
        // the invitee via the grant-approved email (notifyTarget defaults to true).
        //given
        var storage = await CreateHardDriveStorage(
            user: AppOwner,
            encryptionType: StorageEncryptionType.Full);

        var workspace = await CreateWorkspace(
            storage: storage,
            user: AppOwner);

        var invitee = await InviteAndRegisterUser(user: AppOwner);

        await Api.Workspaces.InviteMember(
            externalId: workspace.ExternalId,
            request: new CreateWorkspaceMemberInvitationRequestDto(
                MemberEmails: [invitee.Email],
                AllowShare: false),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery,
            userEncryptionSession: storage.WorkspaceEncryptionSession);

        await Api.Workspaces.AcceptInvitation(
            externalId: workspace.ExternalId,
            cookie: invitee.Cookie,
            antiforgery: invitee.Antiforgery);

        // Invitee configures encryption AFTER accepting the invitation.
        await Api.UserEncryptionPassword.Setup(
            userExternalId: invitee.ExternalId,
            encryptionPassword: "Invitee-Pass-1!",
            cookie: invitee.Cookie,
            antiforgery: invitee.Antiforgery);

        //when — owner manually grants encryption access.
        await Api.Workspaces.GrantEncryptionAccess(
            externalId: workspace.ExternalId,
            memberExternalId: invitee.ExternalId,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery,
            userEncryptionSession: storage.WorkspaceEncryptionSession);

        //then — invitee receives the grant-approved email.
        var (expectedTitle, expectedContent) = Emails.WorkspaceEncryptionKeyGrantApproved(
            applicationName: AppSettings.ApplicationName.Name,
            ownerEmail: AppOwner.Email,
            workspaceName: workspace.Name);

        var expectedHtml = EmailTemplates.Generic.Build(
            title: expectedTitle,
            content: expectedContent);

        await WaitFor(() =>
        {
            ResendEmailServer.ShouldContainEmails([
                new ResendRequestBody(
                    From: EmailProvider.EmailFrom,
                    To: [invitee.Email],
                    Subject: expectedTitle,
                    Html: expectedHtml)
            ]);
        });
    }

    [Fact]
    public async Task single_invite_call_handles_all_invitee_buckets_correctly()
    {
        // Four buckets in one InviteMember call — each is a distinct branch of the
        // resolve-then-insert-then-auto-grant pipeline:
        //  A) already a member of THIS workspace (ON CONFLICT DO NOTHING) — no second
        //     invitation email, no second wek upsert; existing wek must remain valid.
        //  B) registered + has encryption set up — auto-grant fires, ONE invitation
        //     email (no separate "approved" email — the invitation already covers them);
        //     decrypt-after-accept proves the wek wrap landed.
        //  C) registered, no encryption set up — invitation email only; no wek (deferred
        //     path waits for the invitee's own password setup).
        //  D) brand-new email — user invitation row created in the same transaction; the
        //     invitation email carries a registration code that is actually usable.

        //given
        var storage = await CreateHardDriveStorage(
            user: AppOwner,
            encryptionType: StorageEncryptionType.Full);

        var workspace = await CreateWorkspace(
            storage: storage,
            user: AppOwner);

        var folder = await CreateFolder(
            workspace: workspace,
            user: AppOwner);

        var content = Random.Bytes(256);

        var uploadedFile = await UploadFile(
            content: content,
            fileName: $"{Random.Name("file")}.bin",
            contentType: "application/octet-stream",
            folder: folder,
            workspace: workspace,
            user: AppOwner);

        // Bucket A — alice is registered, has encryption, AND was already invited+accepted
        // to this workspace via a prior InviteMember call (which auto-granted her wek).
        var alice = await InviteAndRegisterUser(user: AppOwner);
        var aliceSetup = await Api.UserEncryptionPassword.Setup(
            userExternalId: alice.ExternalId,
            encryptionPassword: "Alice-Pass-1!",
            cookie: alice.Cookie,
            antiforgery: alice.Antiforgery);

        await Api.Workspaces.InviteMember(
            externalId: workspace.ExternalId,
            request: new CreateWorkspaceMemberInvitationRequestDto(
                MemberEmails: [alice.Email],
                AllowShare: false),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery,
            userEncryptionSession: storage.WorkspaceEncryptionSession);

        await Api.Workspaces.AcceptInvitation(
            externalId: workspace.ExternalId,
            cookie: alice.Cookie,
            antiforgery: alice.Antiforgery);

        var aliceWorkspace = workspace with { WorkspaceEncryptionSession = aliceSetup.EncryptionCookie };

        // Sanity: alice can decrypt today (proves her pre-existing wek wrap is valid).
        var aliceDownloadBefore = await DownloadFile(
            fileExternalId: uploadedFile.ExternalId,
            workspace: aliceWorkspace,
            user: alice);
        aliceDownloadBefore.Should().Equal(content);

        // Bucket B — bob is registered + has encryption but is NOT yet a workspace member.
        var bob = await InviteAndRegisterUser(user: AppOwner);
        var bobSetup = await Api.UserEncryptionPassword.Setup(
            userExternalId: bob.ExternalId,
            encryptionPassword: "Bob-Pass-1!",
            cookie: bob.Cookie,
            antiforgery: bob.Antiforgery);

        // Bucket C — carol is registered but has NEVER set up encryption.
        var carol = await InviteAndRegisterUser(user: AppOwner);

        // Bucket D — dave's email has no row in u_users at all. Pre-feed the invitation
        // code mock LAST, so the very next Generate() (the one inside the test InviteMember
        // call) returns this exact code; that's how we know what the email body will carry.
        var daveEmail = Random.Email();
        var daveCode = Random.InvitationCode();
        OneTimeInvitationCode.AddCode(daveCode);

        //when — single InviteMember call covering all four buckets in one transaction.
        await Api.Workspaces.InviteMember(
            externalId: workspace.ExternalId,
            request: new CreateWorkspaceMemberInvitationRequestDto(
                MemberEmails: [alice.Email, bob.Email, carol.Email, daveEmail],
                AllowShare: false),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery,
            userEncryptionSession: storage.WorkspaceEncryptionSession);

        //then — bob, carol and dave each receive the workspace invitation email (carol
        // and bob share the no-code template; dave's body carries the registration code).
        var (workspaceInvitationTitle, inviteContentNoCode) = Emails.WorkspaceMembershipInvitation(
            applicationName: AppSettings.ApplicationName.Name,
            appUrl: AppUrl,
            inviterEmail: AppOwner.Email,
            workspaceName: workspace.Name,
            invitationCode: null);
        var inviteHtmlNoCode = EmailTemplates.Generic.Build(
            title: workspaceInvitationTitle,
            content: inviteContentNoCode);

        var (_, daveInviteContent) = Emails.WorkspaceMembershipInvitation(
            applicationName: AppSettings.ApplicationName.Name,
            appUrl: AppUrl,
            inviterEmail: AppOwner.Email,
            workspaceName: workspace.Name,
            invitationCode: daveCode);
        var daveInviteHtml = EmailTemplates.Generic.Build(
            title: workspaceInvitationTitle,
            content: daveInviteContent);

        await WaitFor(() =>
        {
            ResendEmailServer.ShouldContainEmails([
                new ResendRequestBody(
                    From: EmailProvider.EmailFrom,
                    To: [bob.Email],
                    Subject: workspaceInvitationTitle,
                    Html: inviteHtmlNoCode),
                new ResendRequestBody(
                    From: EmailProvider.EmailFrom,
                    To: [carol.Email],
                    Subject: workspaceInvitationTitle,
                    Html: inviteHtmlNoCode),
                new ResendRequestBody(
                    From: EmailProvider.EmailFrom,
                    To: [daveEmail],
                    Subject: workspaceInvitationTitle,
                    Html: daveInviteHtml)
            ]);
        });

        //then — wek behaviour, proven by file decryption.

        // Bucket A: alice's wek wrap was untouched by the duplicate invite — she still decrypts.
        var aliceDownloadAfter = await DownloadFile(
            fileExternalId: uploadedFile.ExternalId,
            workspace: aliceWorkspace,
            user: alice);
        aliceDownloadAfter.Should().Equal(content);

        // Bucket B: bob auto-granted in the same transaction — accepts and decrypts using
        // his own session.
        await Api.Workspaces.AcceptInvitation(
            externalId: workspace.ExternalId,
            cookie: bob.Cookie,
            antiforgery: bob.Antiforgery);

        var bobDownload = await DownloadFile(
            fileExternalId: uploadedFile.ExternalId,
            workspace: workspace with { WorkspaceEncryptionSession = bobSetup.EncryptionCookie },
            user: bob);
        bobDownload.Should().Equal(content);

        //then — Bucket D: dave's invitation row + invitation code are real (the email body
        // would carry a useless code if the row was missing or the code was rotated).
        var daveAntiforgery = await Api.Antiforgery.GetToken();
        var (daveSignUpResponse, daveCookie) = await Api.Auth.SignUp(
            request: new SignUpUserRequestDto
            {
                Email = daveEmail,
                Password = Random.Password(),
                InvitationCode = daveCode,
                SelectedCheckboxIds = []
            },
            antiforgeryCookies: daveAntiforgery);

        daveSignUpResponse.Should().BeEquivalentTo(SignUpUserResponseDto.SingedUpAndSignedIn);
        daveCookie.Should().NotBeNull();
    }

    [Fact]
    public async Task member_who_leaves_workspace_should_have_wek_revoked()
    {
        //given
        var storage = await CreateHardDriveStorage(
            user: AppOwner,
            encryptionType: StorageEncryptionType.Full);

        var workspace = await CreateWorkspace(
            storage: storage,
            user: AppOwner);

        var invitee = await InviteAndRegisterUser(user: AppOwner);
        await Api.UserEncryptionPassword.Setup(
            userExternalId: invitee.ExternalId,
            encryptionPassword: "Invitee-Pass-1!",
            cookie: invitee.Cookie,
            antiforgery: invitee.Antiforgery);

        await Api.Workspaces.InviteMember(
            externalId: workspace.ExternalId,
            request: new CreateWorkspaceMemberInvitationRequestDto(
                MemberEmails: [invitee.Email],
                AllowShare: false),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery,
            userEncryptionSession: storage.WorkspaceEncryptionSession);

        await Api.Workspaces.AcceptInvitation(
            externalId: workspace.ExternalId,
            cookie: invitee.Cookie,
            antiforgery: invitee.Antiforgery);

        HasWorkspaceEncryptionKey(workspace.ExternalId, invitee.Email).Should().BeTrue(
            "auto-grant should have created a wek for the invitee");

        //when
        await Api.Workspaces.LeaveSharedWorkspace(
            externalId: workspace.ExternalId,
            cookie: invitee.Cookie,
            antiforgery: invitee.Antiforgery);

        //then
        HasWorkspaceEncryptionKey(workspace.ExternalId, invitee.Email).Should().BeFalse(
            "leaving workspace should revoke the member's wek");
    }

    [Fact]
    public async Task member_revoked_by_owner_should_have_wek_revoked()
    {
        //given
        var storage = await CreateHardDriveStorage(
            user: AppOwner,
            encryptionType: StorageEncryptionType.Full);

        var workspace = await CreateWorkspace(
            storage: storage,
            user: AppOwner);

        var invitee = await InviteAndRegisterUser(user: AppOwner);
        await Api.UserEncryptionPassword.Setup(
            userExternalId: invitee.ExternalId,
            encryptionPassword: "Invitee-Pass-1!",
            cookie: invitee.Cookie,
            antiforgery: invitee.Antiforgery);

        await Api.Workspaces.InviteMember(
            externalId: workspace.ExternalId,
            request: new CreateWorkspaceMemberInvitationRequestDto(
                MemberEmails: [invitee.Email],
                AllowShare: false),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery,
            userEncryptionSession: storage.WorkspaceEncryptionSession);

        await Api.Workspaces.AcceptInvitation(
            externalId: workspace.ExternalId,
            cookie: invitee.Cookie,
            antiforgery: invitee.Antiforgery);

        HasWorkspaceEncryptionKey(workspace.ExternalId, invitee.Email).Should().BeTrue(
            "auto-grant should have created a wek for the invitee");

        //when
        await Api.Workspaces.RevokeMember(
            externalId: workspace.ExternalId,
            memberExternalId: invitee.ExternalId,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        HasWorkspaceEncryptionKey(workspace.ExternalId, invitee.Email).Should().BeFalse(
            "revoking a member should revoke their wek");
    }

    [Fact]
    public async Task invitee_who_rejects_invitation_should_have_auto_granted_wek_revoked()
    {
        //given
        var storage = await CreateHardDriveStorage(
            user: AppOwner,
            encryptionType: StorageEncryptionType.Full);

        var workspace = await CreateWorkspace(
            storage: storage,
            user: AppOwner);

        var invitee = await InviteAndRegisterUser(user: AppOwner);
        await Api.UserEncryptionPassword.Setup(
            userExternalId: invitee.ExternalId,
            encryptionPassword: "Invitee-Pass-1!",
            cookie: invitee.Cookie,
            antiforgery: invitee.Antiforgery);

        await Api.Workspaces.InviteMember(
            externalId: workspace.ExternalId,
            request: new CreateWorkspaceMemberInvitationRequestDto(
                MemberEmails: [invitee.Email],
                AllowShare: false),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery,
            userEncryptionSession: storage.WorkspaceEncryptionSession);

        HasWorkspaceEncryptionKey(workspace.ExternalId, invitee.Email).Should().BeTrue(
            "auto-grant should have created a wek for the invitee at invite time");

        //when
        await Api.Workspaces.RejectInvitation(
            externalId: workspace.ExternalId,
            cookie: invitee.Cookie,
            antiforgery: invitee.Antiforgery);

        //then
        HasWorkspaceEncryptionKey(workspace.ExternalId, invitee.Email).Should().BeFalse(
            "rejecting an invitation should revoke the auto-granted wek");
    }

    [Fact]
    public async Task deleting_workspace_should_remove_all_weks()
    {
        //given
        var storage = await CreateHardDriveStorage(
            user: AppOwner,
            encryptionType: StorageEncryptionType.Full);

        var workspace = await CreateWorkspace(
            storage: storage,
            user: AppOwner);

        var acceptedMember = await InviteAndRegisterUser(user: AppOwner);
        await Api.UserEncryptionPassword.Setup(
            userExternalId: acceptedMember.ExternalId,
            encryptionPassword: "Accepted-Pass-1!",
            cookie: acceptedMember.Cookie,
            antiforgery: acceptedMember.Antiforgery);

        var pendingMember = await InviteAndRegisterUser(user: AppOwner);
        await Api.UserEncryptionPassword.Setup(
            userExternalId: pendingMember.ExternalId,
            encryptionPassword: "Pending-Pass-1!",
            cookie: pendingMember.Cookie,
            antiforgery: pendingMember.Antiforgery);

        await Api.Workspaces.InviteMember(
            externalId: workspace.ExternalId,
            request: new CreateWorkspaceMemberInvitationRequestDto(
                MemberEmails: [acceptedMember.Email, pendingMember.Email],
                AllowShare: false),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery,
            userEncryptionSession: storage.WorkspaceEncryptionSession);

        await Api.Workspaces.AcceptInvitation(
            externalId: workspace.ExternalId,
            cookie: acceptedMember.Cookie,
            antiforgery: acceptedMember.Antiforgery);

        HasWorkspaceEncryptionKey(workspace.ExternalId, AppOwner.Email).Should().BeTrue(
            "owner should have a wek");
        HasWorkspaceEncryptionKey(workspace.ExternalId, acceptedMember.Email).Should().BeTrue(
            "accepted member should have a wek from auto-grant");
        HasWorkspaceEncryptionKey(workspace.ExternalId, pendingMember.Email).Should().BeTrue(
            "pending member should have a wek from auto-grant");

        //when
        await Api.Workspaces.Delete(
            externalId: workspace.ExternalId,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await WaitFor(() =>
        {
            HasWorkspaceEncryptionKey(workspace.ExternalId, AppOwner.Email).Should().BeFalse(
                "owner's wek should be removed after workspace deletion");
            HasWorkspaceEncryptionKey(workspace.ExternalId, acceptedMember.Email).Should().BeFalse(
                "accepted member's wek should be removed after workspace deletion");
            HasWorkspaceEncryptionKey(workspace.ExternalId, pendingMember.Email).Should().BeFalse(
                "pending member's wek should be removed after workspace deletion");
        });
    }
}
