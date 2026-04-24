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
        var invitationCode = Random.InvitationCode();
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
        var invitationCode = Random.InvitationCode();
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
        var invitationCode = Random.InvitationCode();
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
        var invitationCode = Random.InvitationCode();
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
        var invitationCode = Random.InvitationCode();
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
        var invitationCode = Random.InvitationCode();
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
        var invitationCode = Random.InvitationCode();
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
        var invitationCode = Random.InvitationCode();
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

    // -- E. Multi-workspace invite of a pending invitee --------------------

    [Fact]
    public async Task two_workspaces_inviting_same_brand_new_invitee_share_ephemeral_public_key()
    {
        // The fix for the original single-code-KEK design: once a brand-new user has
        // been staged, any later invite (to any workspace) reuses the same euek public
        // key. The second invite arrives with InvitationCode == null (the u_users row
        // already exists) and must resolve the pre-existing euek rather than regenerate.
        //given
        var storage = await CreateHardDriveStorage(
            user: AppOwner,
            encryptionType: StorageEncryptionType.Full);

        var workspaceA = await CreateWorkspace(storage: storage, user: AppOwner);
        var workspaceB = await CreateWorkspace(storage: storage, user: AppOwner);

        var inviteeEmail = Random.Email();
        var invitationCode = Random.InvitationCode();
        OneTimeInvitationCode.AddCode(invitationCode);

        //when — first invite creates the euek row.
        await Api.Workspaces.InviteMember(
            externalId: workspaceA.ExternalId,
            request: new CreateWorkspaceMemberInvitationRequestDto(
                MemberEmails: [inviteeEmail],
                AllowShare: false,
                EphemeralDekLifetimeHours: 24),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery,
            userEncryptionSession: storage.WorkspaceEncryptionSession);

        HasEphemeralUserKeyPair(inviteeEmail).Should().BeTrue();
        HasEphemeralWorkspaceEncryptionKey(workspaceA.ExternalId, inviteeEmail).Should().BeTrue();

        var publicKeyAfterFirst = GetEphemeralUserPublicKey(inviteeEmail);
        publicKeyAfterFirst.Should().NotBeNull();

        //when — second invite. u_users row already exists so GetOrCreateUserInvitationQuery
        // returns InvitationCode=null; the ephemeral keypair query must resolve the existing
        // public key without re-requiring the code.
        await Api.Workspaces.InviteMember(
            externalId: workspaceB.ExternalId,
            request: new CreateWorkspaceMemberInvitationRequestDto(
                MemberEmails: [inviteeEmail],
                AllowShare: false,
                EphemeralDekLifetimeHours: 24),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery,
            userEncryptionSession: storage.WorkspaceEncryptionSession);

        //then — both ewek rows exist, sealed to the same shared euek public key.
        HasEphemeralWorkspaceEncryptionKey(workspaceA.ExternalId, inviteeEmail).Should().BeTrue();
        HasEphemeralWorkspaceEncryptionKey(workspaceB.ExternalId, inviteeEmail).Should().BeTrue();

        var publicKeyAfterSecond = GetEphemeralUserPublicKey(inviteeEmail);
        publicKeyAfterSecond.Should().Equal(publicKeyAfterFirst,
            "the second invite must reuse the existing ephemeral user keypair — not regenerate it");
    }

    // -- F. Promotion at encryption-password setup (Phase 2) ----------------

    [Fact]
    public async Task signup_with_invitation_code_promotes_ephemeral_dek_to_wek()
    {
        //given — brand-new invitee, one workspace, ewek + euek staged.
        var storage = await CreateHardDriveStorage(
            user: AppOwner,
            encryptionType: StorageEncryptionType.Full);

        var workspace = await CreateWorkspace(storage: storage, user: AppOwner);

        var inviteeEmail = Random.Email();
        var invitationCode = Random.InvitationCode();
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
        HasEphemeralUserKeyPair(inviteeEmail).Should().BeTrue();
        HasWorkspaceEncryptionKey(workspace.ExternalId, inviteeEmail).Should().BeFalse();

        // Invitee signs up with the invitation code (auto-signed-in).
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
        var inviteeAccount = await Api.Account.GetDetails(inviteeCookie);

        //when — setup forwards the same invitation code; promotion runs atomically
        // with the u_users encryption-metadata write.
        await Api.UserEncryptionPassword.Setup(
            userExternalId: inviteeAccount.ExternalId,
            encryptionPassword: Random.Password(),
            cookie: inviteeCookie,
            antiforgery: inviteeAntiforgery,
            invitationCode: invitationCode);

        //then — wek exists, ewek wiped, euek wiped.
        HasWorkspaceEncryptionKey(workspace.ExternalId, inviteeEmail).Should().BeTrue(
            "setup with invitation code must seal a wek wrap to the just-generated real public key");
        HasEphemeralWorkspaceEncryptionKey(workspace.ExternalId, inviteeEmail).Should().BeFalse(
            "the ephemeral wrap must be deleted once promoted");
        HasEphemeralUserKeyPair(inviteeEmail).Should().BeFalse(
            "the ephemeral user keypair must be wiped once no longer needed");
    }

    [Fact]
    public async Task signup_with_invitation_code_promotes_ephemerals_for_multiple_workspaces()
    {
        // The payoff test for the design fix: one invitation code unlocks access across
        // every workspace the invitee was staged into, in a single setup call.
        //given
        var storage = await CreateHardDriveStorage(
            user: AppOwner,
            encryptionType: StorageEncryptionType.Full);

        var workspaceA = await CreateWorkspace(storage: storage, user: AppOwner);
        var workspaceB = await CreateWorkspace(storage: storage, user: AppOwner);

        var inviteeEmail = Random.Email();
        var invitationCode = Random.InvitationCode();
        OneTimeInvitationCode.AddCode(invitationCode);

        await Api.Workspaces.InviteMember(
            externalId: workspaceA.ExternalId,
            request: new CreateWorkspaceMemberInvitationRequestDto(
                MemberEmails: [inviteeEmail],
                AllowShare: false,
                EphemeralDekLifetimeHours: 24),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery,
            userEncryptionSession: storage.WorkspaceEncryptionSession);

        await Api.Workspaces.InviteMember(
            externalId: workspaceB.ExternalId,
            request: new CreateWorkspaceMemberInvitationRequestDto(
                MemberEmails: [inviteeEmail],
                AllowShare: false,
                EphemeralDekLifetimeHours: 24),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery,
            userEncryptionSession: storage.WorkspaceEncryptionSession);

        var anonymousAntiforgery = await Api.Antiforgery.GetToken();
        var (_, inviteeCookie) = await Api.Auth.SignUp(
            request: new SignUpUserRequestDto
            {
                Email = inviteeEmail,
                Password = Random.Password(),
                InvitationCode = invitationCode,
                SelectedCheckboxIds = []
            },
            antiforgeryCookies: anonymousAntiforgery);

        var inviteeAntiforgery = await Api.Antiforgery.GetToken(inviteeCookie);
        var inviteeAccount = await Api.Account.GetDetails(inviteeCookie);

        //when
        await Api.UserEncryptionPassword.Setup(
            userExternalId: inviteeAccount.ExternalId,
            encryptionPassword: Random.Password(),
            cookie: inviteeCookie,
            antiforgery: inviteeAntiforgery,
            invitationCode: invitationCode);

        //then — all staged workspaces promoted in one setup call.
        HasWorkspaceEncryptionKey(workspaceA.ExternalId, inviteeEmail).Should().BeTrue();
        HasWorkspaceEncryptionKey(workspaceB.ExternalId, inviteeEmail).Should().BeTrue();

        HasEphemeralWorkspaceEncryptionKey(workspaceA.ExternalId, inviteeEmail).Should().BeFalse();
        HasEphemeralWorkspaceEncryptionKey(workspaceB.ExternalId, inviteeEmail).Should().BeFalse();

        HasEphemeralUserKeyPair(inviteeEmail).Should().BeFalse();
    }

    [Fact]
    public async Task invitee_can_download_file_after_signup_promotion()
    {
        // The hard end-to-end proof: bytes come out decrypted, which is only possible
        // if the promoted wek wrap targets the invitee's real public key AND the
        // underlying DEK preserved through two re-wraps (sealed→sealed) is the same
        // plaintext DEK the owner used at encrypt time.
        //given
        var storage = await CreateHardDriveStorage(
            user: AppOwner,
            encryptionType: StorageEncryptionType.Full);

        var workspace = await CreateWorkspace(storage: storage, user: AppOwner);
        var folder = await CreateFolder(workspace: workspace, user: AppOwner);

        var content = Random.Bytes(256);
        var uploadedFile = await UploadFile(
            content: content,
            fileName: $"{Random.Name("file")}.bin",
            contentType: "application/octet-stream",
            folder: folder,
            workspace: workspace,
            user: AppOwner);

        var inviteeEmail = Random.Email();
        var invitationCode = Random.InvitationCode();
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

        var anonymousAntiforgery = await Api.Antiforgery.GetToken();
        var (_, inviteeCookie) = await Api.Auth.SignUp(
            request: new SignUpUserRequestDto
            {
                Email = inviteeEmail,
                Password = Random.Password(),
                InvitationCode = invitationCode,
                SelectedCheckboxIds = []
            },
            antiforgeryCookies: anonymousAntiforgery);

        var inviteeAntiforgery = await Api.Antiforgery.GetToken(inviteeCookie);
        var inviteeAccount = await Api.Account.GetDetails(inviteeCookie);

        var setupResult = await Api.UserEncryptionPassword.Setup(
            userExternalId: inviteeAccount.ExternalId,
            encryptionPassword: Random.Password(),
            cookie: inviteeCookie,
            antiforgery: inviteeAntiforgery,
            invitationCode: invitationCode);

        await Api.Workspaces.AcceptInvitation(
            externalId: workspace.ExternalId,
            cookie: inviteeCookie,
            antiforgery: inviteeAntiforgery);

        //when
        var invitee = new AppSignedInUser(
            ExternalId: inviteeAccount.ExternalId,
            Email: inviteeEmail,
            Password: string.Empty,
            Cookie: inviteeCookie,
            Antiforgery: inviteeAntiforgery,
            EncryptionCookie: setupResult.EncryptionCookie);

        var downloaded = await DownloadFile(
            fileExternalId: uploadedFile.ExternalId,
            workspace: workspace with { WorkspaceEncryptionSession = setupResult.EncryptionCookie },
            user: invitee);

        //then
        downloaded.Should().Equal(content);
    }

    [Fact]
    public async Task setup_without_invitation_code_leaves_ephemeral_for_ttl_cleanup()
    {
        // The invitee skipped the post-signup encryption-password dialog and later
        // opened /account to set it up without the invitation code. Promotion must
        // NOT fire — the ewek stays until its TTL, after which the cleanup job wipes
        // it and the deferred-grant path kicks in (owner must re-grant).
        //given
        var storage = await CreateHardDriveStorage(
            user: AppOwner,
            encryptionType: StorageEncryptionType.Full);

        var workspace = await CreateWorkspace(storage: storage, user: AppOwner);

        var inviteeEmail = Random.Email();
        var invitationCode = Random.InvitationCode();
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

        var anonymousAntiforgery = await Api.Antiforgery.GetToken();
        var (_, inviteeCookie) = await Api.Auth.SignUp(
            request: new SignUpUserRequestDto
            {
                Email = inviteeEmail,
                Password = Random.Password(),
                InvitationCode = invitationCode,
                SelectedCheckboxIds = []
            },
            antiforgeryCookies: anonymousAntiforgery);

        var inviteeAntiforgery = await Api.Antiforgery.GetToken(inviteeCookie);
        var inviteeAccount = await Api.Account.GetDetails(inviteeCookie);

        //when — setup WITHOUT the code.
        await Api.UserEncryptionPassword.Setup(
            userExternalId: inviteeAccount.ExternalId,
            encryptionPassword: Random.Password(),
            cookie: inviteeCookie,
            antiforgery: inviteeAntiforgery,
            invitationCode: null);

        //then — ephemerals untouched, no wek created.
        HasEphemeralWorkspaceEncryptionKey(workspace.ExternalId, inviteeEmail).Should().BeTrue(
            "setup without the code must not promote — ewek stays for the TTL window");
        HasEphemeralUserKeyPair(inviteeEmail).Should().BeTrue(
            "the euek row stays alongside the ewek rows it unlocks");
        HasWorkspaceEncryptionKey(workspace.ExternalId, inviteeEmail).Should().BeFalse();
    }

    [Fact]
    public async Task setup_with_wrong_invitation_code_rolls_back_transaction()
    {
        // A code that is shape-valid but does not match the one that wrapped the
        // ephemeral private key will fail AEAD tag verification during unwrap. Setup
        // must surface a failure and leave u_users untouched — no half-configured
        // encryption identity, no orphaned wek rows.
        //given
        var storage = await CreateHardDriveStorage(
            user: AppOwner,
            encryptionType: StorageEncryptionType.Full);

        var workspace = await CreateWorkspace(storage: storage, user: AppOwner);

        var inviteeEmail = Random.Email();
        var realInvitationCode = Random.InvitationCode();
        OneTimeInvitationCode.AddCode(realInvitationCode);

        await Api.Workspaces.InviteMember(
            externalId: workspace.ExternalId,
            request: new CreateWorkspaceMemberInvitationRequestDto(
                MemberEmails: [inviteeEmail],
                AllowShare: false,
                EphemeralDekLifetimeHours: 24),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery,
            userEncryptionSession: storage.WorkspaceEncryptionSession);

        var anonymousAntiforgery = await Api.Antiforgery.GetToken();
        var (_, inviteeCookie) = await Api.Auth.SignUp(
            request: new SignUpUserRequestDto
            {
                Email = inviteeEmail,
                Password = Random.Password(),
                InvitationCode = realInvitationCode,
                SelectedCheckboxIds = []
            },
            antiforgeryCookies: anonymousAntiforgery);

        var inviteeAntiforgery = await Api.Antiforgery.GetToken(inviteeCookie);
        var inviteeAccount = await Api.Account.GetDetails(inviteeCookie);

        // Different random entropy — same Base62 shape, wrong bytes for the wrap.
        var wrongCode = Random.InvitationCode();

        //when
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(async () =>
            await Api.UserEncryptionPassword.Setup(
                userExternalId: inviteeAccount.ExternalId,
                encryptionPassword: Random.Password(),
                cookie: inviteeCookie,
                antiforgery: inviteeAntiforgery,
                invitationCode: wrongCode));

        //then
        apiError.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);

        var detailsAfter = await Api.Account.GetDetails(inviteeCookie);
        detailsAfter.IsEncryptionConfigured.Should().BeFalse(
            "the whole setup transaction must roll back when promotion throws");

        HasEphemeralWorkspaceEncryptionKey(workspace.ExternalId, inviteeEmail).Should().BeTrue();
        HasEphemeralUserKeyPair(inviteeEmail).Should().BeTrue();
        HasWorkspaceEncryptionKey(workspace.ExternalId, inviteeEmail).Should().BeFalse();
    }

    [Fact]
    public async Task setup_with_code_when_ephemerals_already_expired_succeeds_noop()
    {
        // Invitee waited too long; TTL expired and the cleanup job wiped ewek/euek
        // before they registered. Setup with the code is a no-op on the promotion
        // front — it must still succeed so the user ends up with a working encryption
        // identity, falling into the deferred-grant path from here on.
        //given
        var storage = await CreateHardDriveStorage(
            user: AppOwner,
            encryptionType: StorageEncryptionType.Full);

        var workspace = await CreateWorkspace(storage: storage, user: AppOwner);

        var inviteeEmail = Random.Email();
        var invitationCode = Random.InvitationCode();
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

        // Force the cleanup job to run by advancing the clock; wait for the ewek wipe.
        // The euek row stays (cleanup job intentionally does not GC it — see
        // DeleteEphemeralWorkspaceEncryptionKeysQueueJobExecutor docs).
        Clock.CurrentTime(invitedAt + TimeSpan.FromHours(25));

        await WaitFor(() =>
        {
            HasEphemeralWorkspaceEncryptionKey(workspace.ExternalId, inviteeEmail).Should().BeFalse();
        });

        var anonymousAntiforgery = await Api.Antiforgery.GetToken();
        var (_, inviteeCookie) = await Api.Auth.SignUp(
            request: new SignUpUserRequestDto
            {
                Email = inviteeEmail,
                Password = Random.Password(),
                InvitationCode = invitationCode,
                SelectedCheckboxIds = []
            },
            antiforgeryCookies: anonymousAntiforgery);

        var inviteeAntiforgery = await Api.Antiforgery.GetToken(inviteeCookie);
        var inviteeAccount = await Api.Account.GetDetails(inviteeCookie);

        //when — setup with the code, but nothing is left to promote.
        var setupResult = await Api.UserEncryptionPassword.Setup(
            userExternalId: inviteeAccount.ExternalId,
            encryptionPassword: Random.Password(),
            cookie: inviteeCookie,
            antiforgery: inviteeAntiforgery,
            invitationCode: invitationCode);

        //then — setup succeeded; the user is configured but no wek was created (nothing
        // to promote). Owner-deferred flow takes over from here.
        setupResult.RecoveryCode.Should().NotBeNullOrEmpty();

        var detailsAfter = await Api.Account.GetDetails(inviteeCookie);
        detailsAfter.IsEncryptionConfigured.Should().BeTrue();

        HasWorkspaceEncryptionKey(workspace.ExternalId, inviteeEmail).Should().BeFalse();
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
        var invitationCode = Random.InvitationCode();
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
