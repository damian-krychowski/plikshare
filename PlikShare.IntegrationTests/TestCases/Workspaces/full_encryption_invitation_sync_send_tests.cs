using FluentAssertions;
using PlikShare.Core.Emails;
using PlikShare.EmailProviders.ExternalProviders.Resend;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.Apis;
using PlikShare.Storages.Encryption;
using PlikShare.Workspaces.Members.CreateInvitation.Contracts;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Workspaces;

/// <summary>
/// Covers the synchronous-send + compensating-rollback path that the FE workspace member
/// invite goes through. Plaintext invitation codes double as KEKs for ephemeral DEK wraps,
/// so persisting them in <c>q_queue.q_definition</c> would be a credential leak. The FE
/// path therefore sends the email straight after commit and unwinds the DB if delivery
/// fails — these tests pin that contract.
/// </summary>
[Collection(IntegrationTestsCollection.Name)]
public class full_encryption_invitation_sync_send_tests : TestFixture
{
    private const string WorkspaceMembershipInvitationTemplate = "workspaceMembershipInvitation";

    private AppSignedInUser AppOwner { get; }
    private AppEmailProvider EmailProvider { get; }

    public full_encryption_invitation_sync_send_tests(
        HostFixture8081 hostFixture,
        ITestOutputHelper testOutputHelper)
        : base(hostFixture, testOutputHelper)
    {
        hostFixture.ResetUserEncryption().AsTask().Wait();
        AppOwner = SignIn(user: Users.AppOwner).Result;
        EmailProvider = CreateAndActivateEmailProviderIfMissing(user: AppOwner).Result;

        // Reset failure simulation between tests on the shared fixture.
        ResendEmailServer.ClearFailures();
    }

    [Fact]
    public async Task fe_invite_sends_invitation_email_synchronously_and_does_not_enqueue_a_job()
    {
        //given
        var storage = await CreateHardDriveStorage(
            user: AppOwner,
            encryptionType: StorageEncryptionType.Full);

        var workspace = await CreateWorkspace(
            storage: storage,
            user: AppOwner);

        var inviteeEmail = Random.Email();
        var inviteeCode = Random.InvitationCode();
        OneTimeInvitationCode.AddCode(inviteeCode);

        //when
        await Api.Workspaces.InviteMember(
            externalId: workspace.ExternalId,
            request: new CreateWorkspaceMemberInvitationRequestDto(
                MemberEmails: [inviteeEmail],
                AllowShare: false,
                EphemeralDekLifetimeHours: 24),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery,
            userEncryptionSession: storage.WorkspaceEncryptionSession);

        //then — the sync-send path landed the email at the Resend mock without enqueuing
        // anything. Plaintext code never touches q_queue.q_definition.
        var (expectedTitle, expectedContent) = Emails.WorkspaceMembershipInvitation(
            applicationName: AppSettings.ApplicationName.Name,
            appUrl: AppUrl,
            inviterEmail: AppOwner.Email,
            workspaceName: workspace.Name,
            invitationCode: inviteeCode);

        var expectedHtml = EmailTemplates.Generic.Build(
            title: expectedTitle,
            content: expectedContent);

        ResendEmailServer.ShouldContainEmails([
            new ResendRequestBody(
                From: EmailProvider.EmailFrom,
                To: [inviteeEmail],
                Subject: expectedTitle,
                Html: expectedHtml)
        ]);

        CountInvitationEmailQueueJobsFor(inviteeEmail, WorkspaceMembershipInvitationTemplate)
            .Should()
            .Be(0,
                "FE workspace invitation must NOT be enqueued — the plaintext code persists in " +
                "q_definition (and after success in qc_queue_completed.qc_definition) otherwise");

        // Sanity — the staged DB rows exist as expected.
        HasUserRow(inviteeEmail).Should().BeTrue();
        HasWorkspaceMembership(workspace.ExternalId, inviteeEmail).Should().BeTrue();
        HasEphemeralUserKeyPair(inviteeEmail).Should().BeTrue();
        HasEphemeralWorkspaceEncryptionKey(workspace.ExternalId, inviteeEmail).Should().BeTrue();
    }

    [Fact]
    public async Task fe_invite_rolls_back_db_state_when_email_send_fails()
    {
        //given — a brand-new invitee triggers the full set of stagings: u_users insert,
        // wm row, euek (new), ewek, TTL cleanup job. We force the Resend mock to fail
        // for that recipient so the synchronous-send path triggers the compensating delete.
        var storage = await CreateHardDriveStorage(
            user: AppOwner,
            encryptionType: StorageEncryptionType.Full);

        var workspace = await CreateWorkspace(
            storage: storage,
            user: AppOwner);

        var inviteeEmail = Random.Email();
        OneTimeInvitationCode.AddCode(Random.InvitationCode());

        ResendEmailServer.FailEmailsTo(inviteeEmail);

        try
        {
            //when
            var act = () => Api.Workspaces.InviteMember(
                externalId: workspace.ExternalId,
                request: new CreateWorkspaceMemberInvitationRequestDto(
                    MemberEmails: [inviteeEmail],
                    AllowShare: false,
                    EphemeralDekLifetimeHours: 24),
                cookie: AppOwner.Cookie,
                antiforgery: AppOwner.Antiforgery,
                userEncryptionSession: storage.WorkspaceEncryptionSession);

            //then — endpoint surfaces the failure and DB is fully unwound.
            var exception = await act.Should().ThrowAsync<TestApiCallException>();
            exception.Which.StatusCode.Should().Be(400);
            exception.Which.ResponseBody.Should().Contain("invitation-email-send-failed");

            HasUserRow(inviteeEmail).Should().BeFalse(
                "u_users row was created in this operation and must be deleted on rollback");
            HasWorkspaceMembership(workspace.ExternalId, inviteeEmail).Should().BeFalse();
            HasEphemeralUserKeyPair(inviteeEmail).Should().BeFalse();
            HasEphemeralWorkspaceEncryptionKey(workspace.ExternalId, inviteeEmail).Should().BeFalse();
        }
        finally
        {
            ResendEmailServer.ClearFailures();
        }
    }

    [Fact]
    public async Task fe_invite_keeps_successful_recipients_when_a_later_invitee_email_send_fails()
    {
        //given — two brand-new invitees in one batch; alice's send succeeds, bob's send
        // fails. The synchronous loop stops at bob (any later invitee would also be rolled
        // back) and the compensating delete excludes alice.
        var storage = await CreateHardDriveStorage(
            user: AppOwner,
            encryptionType: StorageEncryptionType.Full);

        var workspace = await CreateWorkspace(
            storage: storage,
            user: AppOwner);

        var aliceEmail = Random.Email();
        var bobEmail = Random.Email();
        OneTimeInvitationCode.AddCodes([
            Random.InvitationCode(),
            Random.InvitationCode()
        ]);

        ResendEmailServer.FailEmailsTo(bobEmail);

        try
        {
            //when
            var act = () => Api.Workspaces.InviteMember(
                externalId: workspace.ExternalId,
                request: new CreateWorkspaceMemberInvitationRequestDto(
                    // Order matters — the operation iterates in input order.
                    MemberEmails: [aliceEmail, bobEmail],
                    AllowShare: false,
                    EphemeralDekLifetimeHours: 24),
                cookie: AppOwner.Cookie,
                antiforgery: AppOwner.Antiforgery,
                userEncryptionSession: storage.WorkspaceEncryptionSession);

            //then — endpoint reports the batch failure to the inviter.
            var exception = await act.Should().ThrowAsync<TestApiCallException>();
            exception.Which.StatusCode.Should().Be(400);
            exception.Which.ResponseBody.Should().Contain("invitation-email-send-failed");

            //then — alice received her email and her staged rows survive the rollback.
            HasUserRow(aliceEmail).Should().BeTrue(
                "alice's send succeeded — her invitation must remain valid");
            HasWorkspaceMembership(workspace.ExternalId, aliceEmail).Should().BeTrue();
            HasEphemeralUserKeyPair(aliceEmail).Should().BeTrue();
            HasEphemeralWorkspaceEncryptionKey(workspace.ExternalId, aliceEmail).Should().BeTrue();

            //then — bob's send failed; his entire artifact set was rolled back.
            HasUserRow(bobEmail).Should().BeFalse();
            HasWorkspaceMembership(workspace.ExternalId, bobEmail).Should().BeFalse();
            HasEphemeralUserKeyPair(bobEmail).Should().BeFalse();
            HasEphemeralWorkspaceEncryptionKey(workspace.ExternalId, bobEmail).Should().BeFalse();
        }
        finally
        {
            ResendEmailServer.ClearFailures();
        }
    }

    [Fact]
    public async Task fe_invite_returns_400_when_no_email_provider_is_active()
    {
        //given
        var storage = await CreateHardDriveStorage(
            user: AppOwner,
            encryptionType: StorageEncryptionType.Full);

        var workspace = await CreateWorkspace(
            storage: storage,
            user: AppOwner);

        var inviteeEmail = Random.Email();

        // Deactivate the active provider — also clears the in-memory EmailProviderStore via
        // the production endpoint path. Restored in finally so other tests still work.
        await Api.EmailProviders.Deactivate(
            emailProviderExternalId: EmailProvider.ExternalId,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        try
        {
            //when
            var act = () => Api.Workspaces.InviteMember(
                externalId: workspace.ExternalId,
                request: new CreateWorkspaceMemberInvitationRequestDto(
                    MemberEmails: [inviteeEmail],
                    AllowShare: false,
                    EphemeralDekLifetimeHours: 24),
                cookie: AppOwner.Cookie,
                antiforgery: AppOwner.Antiforgery,
                userEncryptionSession: storage.WorkspaceEncryptionSession);

            //then — pre-flight fails before any DB write. No invitee row, no membership,
            // no euek, no ewek — nothing to roll back because nothing was staged.
            var exception = await act.Should().ThrowAsync<TestApiCallException>();
            exception.Which.StatusCode.Should().Be(400);
            exception.Which.ResponseBody.Should().Contain("invitation-email-provider-required");

            HasUserRow(inviteeEmail).Should().BeFalse();
            HasWorkspaceMembership(workspace.ExternalId, inviteeEmail).Should().BeFalse();
            HasEphemeralUserKeyPair(inviteeEmail).Should().BeFalse();
            HasEphemeralWorkspaceEncryptionKey(workspace.ExternalId, inviteeEmail).Should().BeFalse();
        }
        finally
        {
            await Api.EmailProviders.Activate(
                emailProviderExternalId: EmailProvider.ExternalId,
                cookie: AppOwner.Cookie,
                antiforgery: AppOwner.Antiforgery);
        }
    }

    [Fact]
    public async Task non_fe_invite_still_enqueues_invitation_email_job()
    {
        //given — same code path BUT non-encrypted storage, so the legacy async-queue
        // delivery is the right behaviour: there is no plaintext-code-as-KEK concern.
        var storage = await CreateHardDriveStorage(
            user: AppOwner,
            encryptionType: StorageEncryptionType.None);

        var workspace = await CreateWorkspace(
            storage: storage,
            user: AppOwner);

        var inviteeEmail = Random.Email();
        OneTimeInvitationCode.AddCode(Random.InvitationCode());

        //when
        await Api.Workspaces.InviteMember(
            externalId: workspace.ExternalId,
            request: new CreateWorkspaceMemberInvitationRequestDto(
                MemberEmails: [inviteeEmail],
                AllowShare: false,
                EphemeralDekLifetimeHours: null),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then — non-FE path still uses the queue.
        CountInvitationEmailQueueJobsFor(inviteeEmail, WorkspaceMembershipInvitationTemplate)
            .Should()
            .BeGreaterThan(0,
                "non-FE workspaces have no plaintext-as-KEK concern — async queue delivery " +
                "is still the right behaviour and must not be regressed by the FE-only sync-send change");

        HasUserRow(inviteeEmail).Should().BeTrue();
        HasWorkspaceMembership(workspace.ExternalId, inviteeEmail).Should().BeTrue();
    }
}
