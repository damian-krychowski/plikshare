using FluentAssertions;
using Microsoft.AspNetCore.Http;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.Apis;
using PlikShare.Storages.Encryption;
using PlikShare.Workspaces.Members.CreateInvitation.Contracts;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Workspaces;

/// <summary>
/// Coverage for the storage-DEK fallback inside
/// <c>ValidateWorkspaceEncryptionSessionFilter</c> /
/// <c>UserContextEncryptionExtensions.UnsealWorkspaceDeks</c>:
///
/// When a caller has no per-workspace wrap in <c>wek_workspace_encryption_keys</c> but
/// does hold a per-storage wrap in <c>sek_storage_encryption_keys</c>, the filter must
/// derive the Workspace DEK on the fly from (Storage DEK, workspace salt) instead of
/// short-circuiting with a 403 pending-key-grant. This is the "storage owner can read
/// every file they could decrypt with the recovery code, even before a workspace member
/// wrap has been provisioned for them" path.
///
/// The test fixture exercises this path by treating the second app owner — who picked
/// up a sek row at storage creation but never received a wek wrap for the workspace —
/// as a stand-in for any sek-holder who is not the workspace creator.
/// </summary>
[Collection(IntegrationTestsCollection.Name)]
public class storage_dek_workspace_dek_derivation_tests : TestFixture
{
    public storage_dek_workspace_dek_derivation_tests(
        HostFixture8081 hostFixture,
        ITestOutputHelper testOutputHelper) : base(hostFixture, testOutputHelper)
    {
        hostFixture.ResetUserEncryption().AsTask().Wait();
    }

    /// <summary>
    /// Sets up encryption for both app owners and creates a full-encryption storage so
    /// both end up with a sek row. The first owner is the storage creator and is also
    /// the user who will create workspaces; the second owner is the sek-holder who has
    /// no wek for those workspaces.
    /// </summary>
    private async Task<(AppSignedInUser FirstOwner, AppSignedInUser SecondOwner, AppStorage Storage)>
        SetupTwoAppOwnersAndFullEncryptedStorage()
    {
        var firstOwnerSignedIn = await SignIn(user: Users.AppOwner);
        var firstOwnerSetup = await Api.UserEncryptionPassword.Setup(
            userExternalId: firstOwnerSignedIn.ExternalId,
            encryptionPassword: DefaultTestEncryptionPassword,
            cookie: firstOwnerSignedIn.Cookie,
            antiforgery: firstOwnerSignedIn.Antiforgery);

        var firstOwner = firstOwnerSignedIn with { EncryptionCookie = firstOwnerSetup.EncryptionCookie };

        var secondOwnerSignedIn = await SignIn(user: Users.SecondAppOwner);
        var secondOwnerSetup = await Api.UserEncryptionPassword.Setup(
            userExternalId: secondOwnerSignedIn.ExternalId,
            encryptionPassword: DefaultTestEncryptionPassword,
            cookie: secondOwnerSignedIn.Cookie,
            antiforgery: secondOwnerSignedIn.Antiforgery);

        var secondOwner = secondOwnerSignedIn with { EncryptionCookie = secondOwnerSetup.EncryptionCookie };

        var storage = await CreateHardDriveStorage(
            user: firstOwner,
            encryptionType: StorageEncryptionType.Full);

        // sanity — both owners must have a sek row, otherwise the storage-DEK
        // derivation path can never fire.
        var sekOwners = GetStorageEncryptionKeyOwnerEmails(storage.ExternalId);
        sekOwners.Should().Contain(firstOwner.Email);
        sekOwners.Should().Contain(secondOwner.Email);

        return (firstOwner, secondOwner, storage);
    }

    [Fact]
    public async Task second_app_owner_can_download_file_via_storage_dek_when_workspace_dek_is_missing()
    {
        // The hard end-to-end proof: bytes come out decrypted for a user who has no wek
        // wrap, which is only possible if the filter actually derived the Workspace DEK
        // from the storage-DEK fallback (HKDF over Storage DEK + workspace salt) rather
        // than short-circuiting with a 403.
        //given
        var (firstOwner, secondOwner, storage) = await SetupTwoAppOwnersAndFullEncryptedStorage();

        var workspace = await CreateWorkspace(
            storage: storage,
            user: firstOwner);

        var folder = await CreateFolder(
            workspace: workspace,
            user: firstOwner);

        var content = Random.Bytes(256);
        var uploadedFile = await UploadFile(
            content: content,
            fileName: $"{Random.Name("file")}.bin",
            contentType: "application/octet-stream",
            folder: folder,
            workspace: workspace,
            user: firstOwner);

        // Pre-condition: only the workspace creator has a wek; the second owner never
        // received one. Both still share the same Storage DEK via their sek rows.
        HasWorkspaceEncryptionKey(workspace.ExternalId, firstOwner.Email).Should().BeTrue(
            "workspace creator always gets a wek at workspace-creation time");
        HasWorkspaceEncryptionKey(workspace.ExternalId, secondOwner.Email).Should().BeFalse(
            "second app owner never received a wek for this workspace — that's the path under test");

        //when — second owner downloads the file using ONLY their own encryption session,
        // i.e. their sek-wrapped Storage DEK is the only key material available to the filter.
        var downloaded = await DownloadFile(
            fileExternalId: uploadedFile.ExternalId,
            workspace: workspace with { WorkspaceEncryptionSession = secondOwner.EncryptionCookie },
            user: secondOwner);

        //then
        downloaded.Should().Equal(content,
            "the Workspace DEK derived from (Storage DEK, workspace salt) must equal the " +
            "DEK the workspace creator used at encrypt time — otherwise the AEAD tag would fail");
    }

    [Fact]
    public async Task second_app_owner_can_upload_file_via_storage_dek_and_workspace_owner_can_decrypt_it()
    {
        // The encrypt-side counterpart: the second owner has no wek, so any file they
        // upload is encrypted under the Workspace DEK derived on the fly from their sek.
        // The workspace creator must be able to decrypt that file via their wek-unwrapped
        // DEK. If the two derivations do not agree, the AEAD tag check fails on download.
        //given
        var (firstOwner, secondOwner, storage) = await SetupTwoAppOwnersAndFullEncryptedStorage();

        var workspace = await CreateWorkspace(
            storage: storage,
            user: firstOwner);

        var folder = await CreateFolder(
            workspace: workspace,
            user: firstOwner);

        var content = Random.Bytes(256);

        //when — second owner uploads using their own session; the filter populates their
        // WorkspaceEncryptionSession via the storage-DEK derivation path.
        var uploadedFile = await UploadFile(
            content: content,
            fileName: $"{Random.Name("file")}.bin",
            contentType: "application/octet-stream",
            folder: folder,
            workspace: workspace with { WorkspaceEncryptionSession = secondOwner.EncryptionCookie },
            user: secondOwner);

        //then — workspace creator can read it back through the wek path. Same DEK on both
        // sides ⇒ tag verifies ⇒ plaintext equals.
        var downloaded = await DownloadFile(
            fileExternalId: uploadedFile.ExternalId,
            workspace: workspace with { WorkspaceEncryptionSession = firstOwner.EncryptionCookie },
            user: firstOwner);

        downloaded.Should().Equal(content);
    }

    [Fact]
    public async Task storage_dek_derivation_does_not_persist_a_workspace_dek_row_for_the_caller()
    {
        // The fallback is intentionally read-only: it materializes a derived Workspace DEK
        // in the per-request session and never inserts a wek row. A persistence side
        // effect here would be problematic — re-derivation is cheap and a stale wek
        // wrap could go out of sync with the user's keypair after a password reset.
        //given
        var (firstOwner, secondOwner, storage) = await SetupTwoAppOwnersAndFullEncryptedStorage();

        var workspace = await CreateWorkspace(
            storage: storage,
            user: firstOwner);

        var folder = await CreateFolder(
            workspace: workspace,
            user: firstOwner);

        var content = Random.Bytes(128);
        var uploadedFile = await UploadFile(
            content: content,
            fileName: $"{Random.Name("file")}.bin",
            contentType: "application/octet-stream",
            folder: folder,
            workspace: workspace,
            user: firstOwner);

        HasWorkspaceEncryptionKey(workspace.ExternalId, secondOwner.Email).Should().BeFalse();

        //when — exercise the fallback once.
        var downloaded = await DownloadFile(
            fileExternalId: uploadedFile.ExternalId,
            workspace: workspace with { WorkspaceEncryptionSession = secondOwner.EncryptionCookie },
            user: secondOwner);

        //then
        downloaded.Should().Equal(content);

        HasWorkspaceEncryptionKey(workspace.ExternalId, secondOwner.Email).Should().BeFalse(
            "the storage-DEK derivation path must not silently materialize a wek row " +
            "for the caller — re-derivation is the contract");
    }

    [Fact]
    public async Task member_without_storage_dek_and_without_workspace_dek_gets_pending_key_grant()
    {
        // The fallback only fires when a sek row exists. A regular invited member who set
        // up encryption but was not granted a wek and is not a storage owner should still
        // hit the 403 pending-key-grant branch — the storage-DEK derivation must NOT be
        // available to non-sek-holders.
        //given
        var (firstOwner, _, storage) = await SetupTwoAppOwnersAndFullEncryptedStorage();
        await CreateAndActivateEmailProviderIfMissing(user: firstOwner);

        var workspace = await CreateWorkspace(
            storage: storage,
            user: firstOwner);

        var folder = await CreateFolder(
            workspace: workspace,
            user: firstOwner);

        var uploadedFile = await UploadFile(
            content: Random.Bytes(64),
            fileName: $"{Random.Name("file")}.bin",
            contentType: "application/octet-stream",
            folder: folder,
            workspace: workspace,
            user: firstOwner);

        // Invitee is registered but has no encryption configured at invite time — so the
        // owner-side auto-grant path cannot wrap a wek for them (no public key to wrap to).
        // After they later set up encryption, the server has their public key but still
        // no wek for this workspace and (because they are not a storage admin) no sek.
        var invitee = await InviteAndRegisterUser(user: firstOwner);

        await Api.Workspaces.InviteMember(
            externalId: workspace.ExternalId,
            request: new CreateWorkspaceMemberInvitationRequestDto(
                MemberEmails: [invitee.Email],
                AllowShare: false,
                EphemeralDekLifetimeHours: null),
            cookie: firstOwner.Cookie,
            antiforgery: firstOwner.Antiforgery,
            userEncryptionSession: storage.WorkspaceEncryptionSession);

        await Api.Workspaces.AcceptInvitation(
            externalId: workspace.ExternalId,
            cookie: invitee.Cookie,
            antiforgery: invitee.Antiforgery);

        var inviteeSetup = await Api.UserEncryptionPassword.Setup(
            userExternalId: invitee.ExternalId,
            encryptionPassword: "Invitee-Pass-1!",
            cookie: invitee.Cookie,
            antiforgery: invitee.Antiforgery);

        HasWorkspaceEncryptionKey(workspace.ExternalId, invitee.Email).Should().BeFalse(
            "no auto-grant fires when the invitee had no public key at workspace-invite time");

        //when
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.Files.GetDownloadLink(
                workspaceExternalId: workspace.ExternalId,
                fileExternalId: uploadedFile.ExternalId,
                contentDisposition: "attachment",
                cookie: invitee.Cookie,
                workspaceEncryptionSession: inviteeSetup.EncryptionCookie));

        //then — no sek means no fallback path. The filter must report pending-key-grant
        // exactly the same way it did before the storage-DEK derivation was added.
        apiError.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        apiError.HttpError.Should().NotBeNull();
        apiError.HttpError!.Code.Should().Be("workspace-encryption-pending-key-grant");
    }

    [Fact]
    public async Task app_owner_without_sek_and_without_wek_gets_pending_key_grant_even_though_they_are_admin()
    {
        // Admin role grants automatic workspace membership (see WorkspaceMembershipCache),
        // so an app owner can pass the membership filter for any workspace. But membership
        // alone is not enough on a full-encrypted workspace — they also need either a wek
        // wrap OR a sek wrap. This test simulates the "second app owner set up encryption
        // AFTER the storage was created" case: they pass the membership check via admin
        // role but have no key material at all and must get pending-key-grant.
        //given
        var firstOwnerSignedIn = await SignIn(user: Users.AppOwner);
        var firstOwnerSetup = await Api.UserEncryptionPassword.Setup(
            userExternalId: firstOwnerSignedIn.ExternalId,
            encryptionPassword: DefaultTestEncryptionPassword,
            cookie: firstOwnerSignedIn.Cookie,
            antiforgery: firstOwnerSignedIn.Antiforgery);

        var firstOwner = firstOwnerSignedIn with { EncryptionCookie = firstOwnerSetup.EncryptionCookie };

        // Storage created BEFORE the second owner set up encryption — so no sek row for
        // them. This is the only way for an app owner to end up with neither wek nor sek.
        var storage = await CreateHardDriveStorage(
            user: firstOwner,
            encryptionType: StorageEncryptionType.Full);

        var secondOwnerSignedIn = await SignIn(user: Users.SecondAppOwner);
        var secondOwnerSetup = await Api.UserEncryptionPassword.Setup(
            userExternalId: secondOwnerSignedIn.ExternalId,
            encryptionPassword: DefaultTestEncryptionPassword,
            cookie: secondOwnerSignedIn.Cookie,
            antiforgery: secondOwnerSignedIn.Antiforgery);

        var secondOwner = secondOwnerSignedIn with { EncryptionCookie = secondOwnerSetup.EncryptionCookie };

        // Sanity — second owner is intentionally NOT a sek holder for this storage.
        var sekOwners = GetStorageEncryptionKeyOwnerEmails(storage.ExternalId);
        sekOwners.Should().NotContain(secondOwner.Email,
            "second owner had no encryption configured at storage-creation time, so no sek row exists");

        var workspace = await CreateWorkspace(
            storage: storage,
            user: firstOwner);

        var folder = await CreateFolder(
            workspace: workspace,
            user: firstOwner);

        var uploadedFile = await UploadFile(
            content: Random.Bytes(64),
            fileName: $"{Random.Name("file")}.bin",
            contentType: "application/octet-stream",
            folder: folder,
            workspace: workspace,
            user: firstOwner);

        HasWorkspaceEncryptionKey(workspace.ExternalId, secondOwner.Email).Should().BeFalse();

        //when
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.Files.GetDownloadLink(
                workspaceExternalId: workspace.ExternalId,
                fileExternalId: uploadedFile.ExternalId,
                contentDisposition: "attachment",
                cookie: secondOwner.Cookie,
                workspaceEncryptionSession: secondOwner.EncryptionCookie));

        //then
        apiError.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        apiError.HttpError.Should().NotBeNull();
        apiError.HttpError!.Code.Should().Be("workspace-encryption-pending-key-grant");
    }
}
