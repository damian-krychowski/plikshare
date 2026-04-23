using FluentAssertions;
using Microsoft.AspNetCore.Http;
using PlikShare.Auth.Contracts;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.Apis;
using PlikShare.Storages.Encryption;
using PlikShare.Workspaces.Members.CreateInvitation.Contracts;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Workspaces;

[Collection(IntegrationTestsCollection.Name)]
public class ephemeral_workspace_dek_tests : TestFixture
{
    private AppSignedInUser AppOwner { get; }

    public ephemeral_workspace_dek_tests(HostFixture8081 hostFixture, ITestOutputHelper testOutputHelper)
        : base(hostFixture, testOutputHelper)
    {
        hostFixture.ResetUserEncryption().AsTask().Wait();
        hostFixture.OneTimeInvitationCode.ClearPredefinedCodes();
        AppOwner = SignIn(user: Users.AppOwner).Result;
        CreateAndActivateEmailProviderIfMissing(user: AppOwner).Wait();
    }

    // -- A. Happy path ------------------------------------------------------

    [Fact]
    public async Task brand_new_invitee_gets_ephemeral_wek_with_expected_ttl()
    {
        //given
        var storage = await CreateHardDriveStorage(
            user: AppOwner,
            encryptionType: StorageEncryptionType.Full);

        var workspace = await CreateWorkspace(
            storage: storage,
            user: AppOwner);

        var inviteeEmail = Random.Email();
        var invitationCode = Random.RealShapeInvitationCode();
        OneTimeInvitationCode.AddCode(invitationCode);

        var invitedAt = DateTimeOffset.UtcNow;
        Clock.CurrentTime(invitedAt);

        //when
        var inviteResponse = await Api.Workspaces.InviteMember(
            externalId: workspace.ExternalId,
            request: new CreateWorkspaceMemberInvitationRequestDto(
                MemberEmails: [inviteeEmail],
                AllowShare: false,
                EphemeralDekLifetimeHours: 24),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery,
            userEncryptionSession: storage.WorkspaceEncryptionSession);

        //then — ewek exists for the brand-new invitee, wek does not.
        HasEphemeralWorkspaceEncryptionKey(workspace.ExternalId, inviteeEmail).Should().BeTrue();
        HasWorkspaceEncryptionKey(workspace.ExternalId, inviteeEmail).Should().BeFalse();

        // expires_at lands where the owner asked for it (± a small tolerance for DB roundtrip).
        var expiresAt = GetEphemeralWorkspaceEncryptionKeyExpiresAt(workspace.ExternalId, inviteeEmail);
        expiresAt.Should().NotBeNull();
        expiresAt!.Value.Should().BeCloseTo(invitedAt + TimeSpan.FromHours(24), TimeSpan.FromSeconds(5));

        // Cleanup job staged on the same (workspace, user) debounce, fires at the same TTL.
        var memberId = inviteResponse.Members[0].ExternalId;
        var debounceId = BuildEphemeralCleanupDebounceId(workspace.ExternalId, inviteeEmail);

        var (jobCount, executeAfter) = GetCleanupJobInfo(debounceId);
        jobCount.Should().Be(1);
        executeAfter.Should().NotBeNull();
        executeAfter!.Value.Should().BeCloseTo(invitedAt + TimeSpan.FromHours(24), TimeSpan.FromSeconds(5));

        // keep reference to silence analyzer on unused member id — it's useful for anyone
        // reading the log output above.
        _ = memberId;
    }

    // -- B. Validation ------------------------------------------------------

    [Fact]
    public async Task invite_without_ttl_when_ephemeral_required_throws_error()
    {
        // Full-encryption + brand-new invitee + TTL null ⇒ operation throws.
        // The endpoint-level validator only rejects out-of-range TTLs; null passes through
        // and the operation raises InvalidOperationException as a defense-in-depth check.
        //given
        var storage = await CreateHardDriveStorage(
            user: AppOwner,
            encryptionType: StorageEncryptionType.Full);

        var workspace = await CreateWorkspace(
            storage: storage,
            user: AppOwner);

        var inviteeEmail = Random.Email();
        var invitationCode = Random.RealShapeInvitationCode();
        OneTimeInvitationCode.AddCode(invitationCode);

        //when
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.Workspaces.InviteMember(
                externalId: workspace.ExternalId,
                request: new CreateWorkspaceMemberInvitationRequestDto(
                    MemberEmails: [inviteeEmail],
                    AllowShare: false,
                    EphemeralDekLifetimeHours: null),
                cookie: AppOwner.Cookie,
                antiforgery: AppOwner.Antiforgery,
                userEncryptionSession: storage.WorkspaceEncryptionSession));

        //then
        apiError.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);

        // Transaction rolled back — no orphan ewek, no orphan cleanup job.
        HasEphemeralWorkspaceEncryptionKey(workspace.ExternalId, inviteeEmail).Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(721)]
    [InlineData(100000)]
    public async Task invite_with_ttl_out_of_range_returns_400(int outOfRangeHours)
    {
        //given
        var storage = await CreateHardDriveStorage(
            user: AppOwner,
            encryptionType: StorageEncryptionType.Full);

        var workspace = await CreateWorkspace(
            storage: storage,
            user: AppOwner);

        var inviteeEmail = Random.Email();
        var invitationCode = Random.RealShapeInvitationCode();
        OneTimeInvitationCode.AddCode(invitationCode);

        //when
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.Workspaces.InviteMember(
                externalId: workspace.ExternalId,
                request: new CreateWorkspaceMemberInvitationRequestDto(
                    MemberEmails: [inviteeEmail],
                    AllowShare: false,
                    EphemeralDekLifetimeHours: outOfRangeHours),
                cookie: AppOwner.Cookie,
                antiforgery: AppOwner.Antiforgery,
                userEncryptionSession: storage.WorkspaceEncryptionSession));

        //then
        apiError.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        apiError.HttpError.Should().NotBeNull();
        apiError.HttpError!.Code.Should().Be("invalid-ephemeral-dek-lifetime");

        HasEphemeralWorkspaceEncryptionKey(workspace.ExternalId, inviteeEmail).Should().BeFalse();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(24)]
    [InlineData(48)]
    [InlineData(168)]
    [InlineData(720)]
    public async Task invite_with_valid_ttl_boundary_values_accepted(int validHours)
    {
        //given
        var storage = await CreateHardDriveStorage(
            user: AppOwner,
            encryptionType: StorageEncryptionType.Full);

        var workspace = await CreateWorkspace(
            storage: storage,
            user: AppOwner);

        var inviteeEmail = Random.Email();
        var invitationCode = Random.RealShapeInvitationCode();
        OneTimeInvitationCode.AddCode(invitationCode);

        var invitedAt = DateTimeOffset.UtcNow;
        Clock.CurrentTime(invitedAt);

        //when
        await Api.Workspaces.InviteMember(
            externalId: workspace.ExternalId,
            request: new CreateWorkspaceMemberInvitationRequestDto(
                MemberEmails: [inviteeEmail],
                AllowShare: false,
                EphemeralDekLifetimeHours: validHours),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery,
            userEncryptionSession: storage.WorkspaceEncryptionSession);

        //then
        HasEphemeralWorkspaceEncryptionKey(workspace.ExternalId, inviteeEmail).Should().BeTrue();

        var expiresAt = GetEphemeralWorkspaceEncryptionKeyExpiresAt(workspace.ExternalId, inviteeEmail);
        expiresAt.Should().NotBeNull();
        expiresAt!.Value.Should().BeCloseTo(invitedAt + TimeSpan.FromHours(validHours), TimeSpan.FromSeconds(5));
    }

    // -- C. Cleanup job -----------------------------------------------------

    [Fact]
    public async Task cleanup_job_deletes_ephemeral_wek_after_ttl()
    {
        //given
        var storage = await CreateHardDriveStorage(
            user: AppOwner,
            encryptionType: StorageEncryptionType.Full);

        var workspace = await CreateWorkspace(
            storage: storage,
            user: AppOwner);

        var inviteeEmail = Random.Email();
        var invitationCode = Random.RealShapeInvitationCode();
        OneTimeInvitationCode.AddCode(invitationCode);

        var invitedAt = DateTimeOffset.UtcNow;
        Clock.CurrentTime(invitedAt);

        await Api.Workspaces.InviteMember(
            externalId: workspace.ExternalId,
            request: new CreateWorkspaceMemberInvitationRequestDto(
                MemberEmails: [inviteeEmail],
                AllowShare: false,
                EphemeralDekLifetimeHours: 24),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery,
            userEncryptionSession: storage.WorkspaceEncryptionSession);

        HasEphemeralWorkspaceEncryptionKey(workspace.ExternalId, inviteeEmail).Should().BeTrue();

        //when — jump past the TTL so the producer starts picking the job up.
        Clock.CurrentTime(invitedAt + TimeSpan.FromHours(25));

        //then
        await WaitFor(() =>
        {
            HasEphemeralWorkspaceEncryptionKey(workspace.ExternalId, inviteeEmail).Should().BeFalse();
        });
    }

    [Fact]
    public async Task cleanup_job_is_idempotent_after_manual_deletion()
    {
        // The ewek row can be consumed early by the encryption-password-setup promotion
        // path (stage 2 of this feature). When the cleanup job finally fires, its DELETE
        // matches zero rows — the job must still succeed so the queue does not retry
        // forever and fill the logs with phantom errors.
        //given
        var storage = await CreateHardDriveStorage(
            user: AppOwner,
            encryptionType: StorageEncryptionType.Full);

        var workspace = await CreateWorkspace(
            storage: storage,
            user: AppOwner);

        var inviteeEmail = Random.Email();
        var invitationCode = Random.RealShapeInvitationCode();
        OneTimeInvitationCode.AddCode(invitationCode);

        var invitedAt = DateTimeOffset.UtcNow;
        Clock.CurrentTime(invitedAt);

        await Api.Workspaces.InviteMember(
            externalId: workspace.ExternalId,
            request: new CreateWorkspaceMemberInvitationRequestDto(
                MemberEmails: [inviteeEmail],
                AllowShare: false,
                EphemeralDekLifetimeHours: 24),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery,
            userEncryptionSession: storage.WorkspaceEncryptionSession);

        // Simulate an earlier consumer (promotion) having already wiped the ewek row.
        var manuallyDeleted = DeleteEphemeralWorkspaceEncryptionKeys(workspace.ExternalId, inviteeEmail);
        manuallyDeleted.Should().BeGreaterThan(0);

        var debounceId = BuildEphemeralCleanupDebounceId(workspace.ExternalId, inviteeEmail);
        var (preRunJobCount, _) = GetCleanupJobInfo(debounceId);
        preRunJobCount.Should().Be(1, "the cleanup job is still queued even though the ewek row is gone");

        //when — advance the clock so the producer picks the job up.
        Clock.CurrentTime(invitedAt + TimeSpan.FromHours(25));

        //then — the job completes successfully and is removed from the queue.
        await WaitFor(() =>
        {
            var (postRunJobCount, _) = GetCleanupJobInfo(debounceId);
            postRunJobCount.Should().Be(0, "the job should be marked as successfully completed and evicted from the queue");
        });

        // Sanity: nothing re-appeared in ewek.
        HasEphemeralWorkspaceEncryptionKey(workspace.ExternalId, inviteeEmail).Should().BeFalse();
    }

    // -- D. Cascade and owner actions ---------------------------------------

    [Fact]
    public async Task deleting_workspace_removes_ephemeral_weks()
    {
        //given
        var storage = await CreateHardDriveStorage(
            user: AppOwner,
            encryptionType: StorageEncryptionType.Full);

        var workspace = await CreateWorkspace(
            storage: storage,
            user: AppOwner);

        var inviteeEmail = Random.Email();
        var invitationCode = Random.RealShapeInvitationCode();
        OneTimeInvitationCode.AddCode(invitationCode);

        await Api.Workspaces.InviteMember(
            externalId: workspace.ExternalId,
            request: new CreateWorkspaceMemberInvitationRequestDto(
                MemberEmails: [inviteeEmail],
                AllowShare: false,
                EphemeralDekLifetimeHours: 24),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery,
            userEncryptionSession: storage.WorkspaceEncryptionSession);

        HasEphemeralWorkspaceEncryptionKey(workspace.ExternalId, inviteeEmail).Should().BeTrue();

        //when
        await Api.Workspaces.Delete(
            externalId: workspace.ExternalId,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then — ON DELETE CASCADE on ewek_workspace_id wipes the staged wrap.
        await WaitFor(() =>
        {
            HasEphemeralWorkspaceEncryptionKey(workspace.ExternalId, inviteeEmail).Should().BeFalse();
        });
    }

    [Fact]
    public async Task owner_revoke_before_invitation_accepted_removes_ephemeral_wek()
    {
        // Cancel-pending-invitation path. The invitee never logs in; the owner changes
        // their mind and revokes. The staged ephemeral DEK is a live credential until
        // its TTL — revoke must wipe it immediately, not wait for the cleanup job.
        //given
        var storage = await CreateHardDriveStorage(
            user: AppOwner,
            encryptionType: StorageEncryptionType.Full);

        var workspace = await CreateWorkspace(
            storage: storage,
            user: AppOwner);

        var inviteeEmail = Random.Email();
        var invitationCode = Random.RealShapeInvitationCode();
        OneTimeInvitationCode.AddCode(invitationCode);

        var inviteResponse = await Api.Workspaces.InviteMember(
            externalId: workspace.ExternalId,
            request: new CreateWorkspaceMemberInvitationRequestDto(
                MemberEmails: [inviteeEmail],
                AllowShare: false,
                EphemeralDekLifetimeHours: 24),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery,
            userEncryptionSession: storage.WorkspaceEncryptionSession);

        var memberExternalId = inviteResponse.Members[0].ExternalId;

        HasEphemeralWorkspaceEncryptionKey(workspace.ExternalId, inviteeEmail).Should().BeTrue();

        //when — owner revokes the pending invitation (same endpoint covers the revoke
        // of accepted members as well; path is shared).
        await Api.Workspaces.RevokeMember(
            externalId: workspace.ExternalId,
            memberExternalId: memberExternalId,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        HasEphemeralWorkspaceEncryptionKey(workspace.ExternalId, inviteeEmail).Should().BeFalse(
            "revoking a pending invitation must wipe the ephemeral DEK immediately");
    }

    [Fact]
    public async Task rejected_invitation_still_cleaned_up_by_ttl_job()
    {
        // Reject intentionally does NOT wipe the ewek — the invitee may change their
        // mind or be re-invited, and the TTL window is what caps our exposure. The
        // cleanup job must still fire at the scheduled time regardless of rejection.
        //given
        var storage = await CreateHardDriveStorage(
            user: AppOwner,
            encryptionType: StorageEncryptionType.Full);

        var workspace = await CreateWorkspace(
            storage: storage,
            user: AppOwner);

        var inviteeEmail = Random.Email();
        var invitationCode = Random.RealShapeInvitationCode();
        OneTimeInvitationCode.AddCode(invitationCode);

        var invitedAt = DateTimeOffset.UtcNow;
        Clock.CurrentTime(invitedAt);

        await Api.Workspaces.InviteMember(
            externalId: workspace.ExternalId,
            request: new CreateWorkspaceMemberInvitationRequestDto(
                MemberEmails: [inviteeEmail],
                AllowShare: false,
                EphemeralDekLifetimeHours: 24),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery,
            userEncryptionSession: storage.WorkspaceEncryptionSession);

        // Invitee registers with the invitation code they received in the email, then
        // rejects the workspace invitation.
        var anonymousAntiforgery = await Api.Antiforgery.GetToken();

        var (signUpResponse, inviteeCookie) = await Api.Auth.SignUp(
            request: new SignUpUserRequestDto
            {
                Email = inviteeEmail,
                Password = Random.Password(),
                InvitationCode = invitationCode,
                SelectedCheckboxIds = []
            },
            antiforgeryCookies: anonymousAntiforgery);

        signUpResponse.Should().BeEquivalentTo(SignUpUserResponseDto.SingedUpAndSignedIn);

        var inviteeAntiforgery = await Api.Antiforgery.GetToken(inviteeCookie);

        await Api.Workspaces.RejectInvitation(
            externalId: workspace.ExternalId,
            cookie: inviteeCookie,
            antiforgery: inviteeAntiforgery);

        // Sanity — reject on its own leaves the ewek alone; the TTL job is in charge.
        HasEphemeralWorkspaceEncryptionKey(workspace.ExternalId, inviteeEmail).Should().BeTrue(
            "reject alone does not wipe the ewek — that's delegated to the TTL cleanup job");

        //when — fast-forward past the TTL.
        Clock.CurrentTime(invitedAt + TimeSpan.FromHours(25));

        //then — cleanup job runs and removes the ewek even though the invitation was rejected.
        await WaitFor(() =>
        {
            HasEphemeralWorkspaceEncryptionKey(workspace.ExternalId, inviteeEmail).Should().BeFalse();
        });
    }

}
