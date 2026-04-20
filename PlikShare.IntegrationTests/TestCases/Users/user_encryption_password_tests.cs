using FluentAssertions;
using Microsoft.AspNetCore.Http;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.Apis;
using PlikShare.Storages.Encryption;
using PlikShare.Workspaces.Members.CreateInvitation.Contracts;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Users;

[Collection(IntegrationTestsCollection.Name)]
public class user_encryption_password_tests : TestFixture
{
    private AppSignedInUser AppOwner { get; }

    public user_encryption_password_tests(
        HostFixture8081 hostFixture,
        ITestOutputHelper testOutputHelper) : base(hostFixture, testOutputHelper)
    {
        hostFixture.ResetUserEncryption().AsTask().Wait();
        AppOwner = SignIn(user: Users.AppOwner).Result;
    }

    [Fact]
    public async Task setup_returns_recovery_code_and_encryption_session_cookie()
    {
        //when
        var result = await Api.UserEncryptionPassword.Setup(
            userExternalId: AppOwner.ExternalId,
            encryptionPassword: DefaultTestEncryptionPassword,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        result.RecoveryCode.Should().NotBeNullOrWhiteSpace();

        var words = result.RecoveryCode.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        words.Should().HaveCount(24);

        result.EncryptionCookie.Should().NotBeNull();
        result.EncryptionCookie.Name.Should().Be($"UserEncryptionSession_{AppOwner.ExternalId}");
        result.EncryptionCookie.Value.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task setup_called_twice_fails_with_already_configured()
    {
        //given
        await Api.UserEncryptionPassword.Setup(
            userExternalId: AppOwner.ExternalId,
            encryptionPassword: DefaultTestEncryptionPassword,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //when
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.UserEncryptionPassword.Setup(
                userExternalId: AppOwner.ExternalId,
                encryptionPassword: "Different-Password-2!",
                cookie: AppOwner.Cookie,
                antiforgery: AppOwner.Antiforgery));

        //then
        apiError.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        apiError.HttpError.Should().NotBeNull();
        apiError.HttpError!.Code.Should().Be("user-encryption-already-configured");
    }

    [Fact]
    public async Task unlock_with_correct_password_returns_new_encryption_session_cookie()
    {
        //given
        await Api.UserEncryptionPassword.Setup(
            userExternalId: AppOwner.ExternalId,
            encryptionPassword: DefaultTestEncryptionPassword,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //when
        var cookie = await Api.UserEncryptionPassword.Unlock(
            userExternalId: AppOwner.ExternalId,
            encryptionPassword: DefaultTestEncryptionPassword,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        cookie.Should().NotBeNull();
        cookie.Name.Should().Be($"UserEncryptionSession_{AppOwner.ExternalId}");
        cookie.Value.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task unlock_with_wrong_password_fails_with_invalid_password()
    {
        //given
        await Api.UserEncryptionPassword.Setup(
            userExternalId: AppOwner.ExternalId,
            encryptionPassword: DefaultTestEncryptionPassword,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //when
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.UserEncryptionPassword.Unlock(
                userExternalId: AppOwner.ExternalId,
                encryptionPassword: "Wrong-Password-1!",
                cookie: AppOwner.Cookie,
                antiforgery: AppOwner.Antiforgery));

        //then
        apiError.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        apiError.HttpError.Should().NotBeNull();
        apiError.HttpError!.Code.Should().Be("invalid-encryption-password");
    }

    [Fact]
    public async Task unlock_before_setup_fails_with_not_configured()
    {
        //when
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.UserEncryptionPassword.Unlock(
                userExternalId: AppOwner.ExternalId,
                encryptionPassword: DefaultTestEncryptionPassword,
                cookie: AppOwner.Cookie,
                antiforgery: AppOwner.Antiforgery));

        //then
        apiError.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        apiError.HttpError.Should().NotBeNull();
        apiError.HttpError!.Code.Should().Be("user-encryption-not-configured");
    }

    [Fact]
    public async Task setup_helper_attaches_encryption_cookie_to_user()
    {
        //when
        var (updatedUser, recoveryCode) = await SetupUserEncryptionPassword(AppOwner);

        //then
        updatedUser.EncryptionCookie.Should().NotBeNull();
        recoveryCode.Should().NotBeNullOrWhiteSpace();
        recoveryCode.Split(' ', StringSplitOptions.RemoveEmptyEntries).Should().HaveCount(24);
    }

    [Fact]
    public async Task change_with_correct_old_password_returns_new_encryption_session_cookie()
    {
        //given
        await Api.UserEncryptionPassword.Setup(
            userExternalId: AppOwner.ExternalId,
            encryptionPassword: DefaultTestEncryptionPassword,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //when
        var cookie = await Api.UserEncryptionPassword.Change(
            userExternalId: AppOwner.ExternalId,
            oldPassword: DefaultTestEncryptionPassword,
            newPassword: "New-Encryption-Password-1!",
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        cookie.Should().NotBeNull();
        cookie.Name.Should().Be($"UserEncryptionSession_{AppOwner.ExternalId}");
        cookie.Value.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task change_with_wrong_old_password_fails_with_invalid_old_encryption_password()
    {
        //given
        await Api.UserEncryptionPassword.Setup(
            userExternalId: AppOwner.ExternalId,
            encryptionPassword: DefaultTestEncryptionPassword,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //when
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.UserEncryptionPassword.Change(
                userExternalId: AppOwner.ExternalId,
                oldPassword: "Wrong-Password-1!",
                newPassword: "New-Encryption-Password-1!",
                cookie: AppOwner.Cookie,
                antiforgery: AppOwner.Antiforgery));

        //then
        apiError.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        apiError.HttpError.Should().NotBeNull();
        apiError.HttpError!.Code.Should().Be("invalid-old-encryption-password");
    }

    [Fact]
    public async Task change_before_setup_fails_with_not_configured()
    {
        //when
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.UserEncryptionPassword.Change(
                userExternalId: AppOwner.ExternalId,
                oldPassword: DefaultTestEncryptionPassword,
                newPassword: "New-Encryption-Password-1!",
                cookie: AppOwner.Cookie,
                antiforgery: AppOwner.Antiforgery));

        //then
        apiError.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        apiError.HttpError.Should().NotBeNull();
        apiError.HttpError!.Code.Should().Be("user-encryption-not-configured");
    }

    [Fact]
    public async Task after_change_unlock_with_new_password_succeeds_and_old_password_fails()
    {
        //given
        const string newPassword = "New-Encryption-Password-1!";

        await Api.UserEncryptionPassword.Setup(
            userExternalId: AppOwner.ExternalId,
            encryptionPassword: DefaultTestEncryptionPassword,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        await Api.UserEncryptionPassword.Change(
            userExternalId: AppOwner.ExternalId,
            oldPassword: DefaultTestEncryptionPassword,
            newPassword: newPassword,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //when
        var cookie = await Api.UserEncryptionPassword.Unlock(
            userExternalId: AppOwner.ExternalId,
            encryptionPassword: newPassword,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.UserEncryptionPassword.Unlock(
                userExternalId: AppOwner.ExternalId,
                encryptionPassword: DefaultTestEncryptionPassword,
                cookie: AppOwner.Cookie,
                antiforgery: AppOwner.Antiforgery));

        //then
        cookie.Should().NotBeNull();
        cookie.Value.Should().NotBeNullOrWhiteSpace();

        apiError.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        apiError.HttpError!.Code.Should().Be("invalid-encryption-password");
    }

    [Fact]
    public async Task after_change_previously_uploaded_full_encrypted_file_is_still_downloadable()
    {
        //given
        const string newPassword = "New-Encryption-Password-1!";

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

        //when
        var newEncryptionCookie = await Api.UserEncryptionPassword.Change(
            userExternalId: AppOwner.ExternalId,
            oldPassword: DefaultTestEncryptionPassword,
            newPassword: newPassword,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        var workspaceAfterChange = workspace with { WorkspaceEncryptionSession = newEncryptionCookie };

        var downloaded = await DownloadFile(
            fileExternalId: uploadedFile.ExternalId,
            workspace: workspaceAfterChange,
            user: AppOwner);

        //then
        downloaded.Should().Equal(content);
    }

    [Fact]
    public async Task reset_with_correct_recovery_code_returns_new_encryption_session_cookie()
    {
        //given
        var (_, recoveryCode) = await SetupUserEncryptionPassword(AppOwner);

        //when
        var cookie = await Api.UserEncryptionPassword.Reset(
            userExternalId: AppOwner.ExternalId,
            recoveryCode: recoveryCode,
            newPassword: "New-Encryption-Password-1!",
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        cookie.Should().NotBeNull();
        cookie.Name.Should().Be($"UserEncryptionSession_{AppOwner.ExternalId}");
        cookie.Value.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task reset_with_wrong_recovery_code_fails_with_invalid_recovery_code()
    {
        //given
        await SetupUserEncryptionPassword(AppOwner);

        var wrongRecoveryCode = string.Join(' ', Enumerable.Repeat("abandon", 24));

        //when
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.UserEncryptionPassword.Reset(
                userExternalId: AppOwner.ExternalId,
                recoveryCode: wrongRecoveryCode,
                newPassword: "New-Encryption-Password-1!",
                cookie: AppOwner.Cookie,
                antiforgery: AppOwner.Antiforgery));

        //then
        apiError.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        apiError.HttpError.Should().NotBeNull();
        apiError.HttpError!.Code.Should().Be("invalid-recovery-code");
    }

    [Fact]
    public async Task reset_before_setup_fails_with_not_configured()
    {
        //given
        var recoveryCode = string.Join(' ', Enumerable.Repeat("abandon", 24));

        //when
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.UserEncryptionPassword.Reset(
                userExternalId: AppOwner.ExternalId,
                recoveryCode: recoveryCode,
                newPassword: "New-Encryption-Password-1!",
                cookie: AppOwner.Cookie,
                antiforgery: AppOwner.Antiforgery));

        //then
        apiError.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        apiError.HttpError.Should().NotBeNull();
        apiError.HttpError!.Code.Should().Be("user-encryption-not-configured");
    }

    [Fact]
    public async Task after_reset_unlock_with_new_password_succeeds_and_old_password_fails()
    {
        //given
        const string newPassword = "New-Encryption-Password-1!";

        var (_, recoveryCode) = await SetupUserEncryptionPassword(AppOwner);

        await Api.UserEncryptionPassword.Reset(
            userExternalId: AppOwner.ExternalId,
            recoveryCode: recoveryCode,
            newPassword: newPassword,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //when
        var cookie = await Api.UserEncryptionPassword.Unlock(
            userExternalId: AppOwner.ExternalId,
            encryptionPassword: newPassword,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.UserEncryptionPassword.Unlock(
                userExternalId: AppOwner.ExternalId,
                encryptionPassword: DefaultTestEncryptionPassword,
                cookie: AppOwner.Cookie,
                antiforgery: AppOwner.Antiforgery));

        //then
        cookie.Should().NotBeNull();
        cookie.Value.Should().NotBeNullOrWhiteSpace();

        apiError.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        apiError.HttpError!.Code.Should().Be("invalid-encryption-password");
    }

    [Fact]
    public async Task after_reset_previously_uploaded_full_encrypted_file_is_still_downloadable()
    {
        //given
        const string newPassword = "New-Encryption-Password-1!";

        var (owner, recoveryCode) = await SetupUserEncryptionPassword(AppOwner);

        var storage = await CreateHardDriveStorage(
            user: owner,
            encryptionType: StorageEncryptionType.Full);

        var workspace = await CreateWorkspace(
            storage: storage,
            user: owner);

        var folder = await CreateFolder(
            workspace: workspace,
            user: owner);

        var content = Random.Bytes(256);

        var uploadedFile = await UploadFile(
            content: content,
            fileName: $"{Random.Name("file")}.bin",
            contentType: "application/octet-stream",
            folder: folder,
            workspace: workspace,
            user: owner);

        //when
        var newEncryptionCookie = await Api.UserEncryptionPassword.Reset(
            userExternalId: owner.ExternalId,
            recoveryCode: recoveryCode,
            newPassword: newPassword,
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        var workspaceAfterReset = workspace with { WorkspaceEncryptionSession = newEncryptionCookie };

        var downloaded = await DownloadFile(
            fileExternalId: uploadedFile.ExternalId,
            workspace: workspaceAfterReset,
            user: owner);

        //then
        downloaded.Should().Equal(content);
    }

    [Fact]
    public async Task after_sign_out_and_sign_in_full_workspace_access_without_unlock_fails_with_423()
    {
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

        // fresh sign-in gives a new session cookie and NO encryption cookie
        var freshOwner = await SignIn(user: Users.AppOwner);

        //when
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.Files.GetDownloadLink(
                workspaceExternalId: workspace.ExternalId,
                fileExternalId: uploadedFile.ExternalId,
                contentDisposition: "attachment",
                cookie: freshOwner.Cookie,
                workspaceEncryptionSession: null));

        //then
        apiError.StatusCode.Should().Be(StatusCodes.Status423Locked);
        apiError.HttpError.Should().NotBeNull();
        apiError.HttpError!.Code.Should().Be("user-encryption-session-required");
    }

    [Fact]
    public async Task unlock_after_sign_in_restores_access_to_previously_uploaded_full_encrypted_file()
    {
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

        // fresh sign-in gives a new session cookie and NO encryption cookie
        var freshOwner = await SignIn(user: Users.AppOwner);

        //when
        var encryptionCookie = await Api.UserEncryptionPassword.Unlock(
            userExternalId: freshOwner.ExternalId,
            encryptionPassword: DefaultTestEncryptionPassword,
            cookie: freshOwner.Cookie,
            antiforgery: freshOwner.Antiforgery);

        var workspaceWithUnlock = workspace with { WorkspaceEncryptionSession = encryptionCookie };

        var downloaded = await DownloadFile(
            fileExternalId: uploadedFile.ExternalId,
            workspace: workspaceWithUnlock,
            user: freshOwner);

        //then
        downloaded.Should().Equal(content);
    }

    [Fact]
    public async Task after_lock_full_workspace_access_fails_with_423()
    {
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

        //when
        await Api.UserEncryptionPassword.Lock(
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.Files.GetDownloadLink(
                workspaceExternalId: workspace.ExternalId,
                fileExternalId: uploadedFile.ExternalId,
                contentDisposition: "attachment",
                cookie: AppOwner.Cookie,
                workspaceEncryptionSession: null));

        apiError.StatusCode.Should().Be(StatusCodes.Status423Locked);
        apiError.HttpError.Should().NotBeNull();
        apiError.HttpError!.Code.Should().Be("user-encryption-session-required");
    }

    [Fact]
    public async Task invited_user_without_encryption_setup_accessing_full_encrypted_workspace_fails_with_setup_required()
    {
        //given — owner owns a full-encrypted workspace with a file in it
        var storage = await CreateHardDriveStorage(
            user: AppOwner,
            encryptionType: StorageEncryptionType.Full);

        var workspace = await CreateWorkspace(
            storage: storage,
            user: AppOwner);

        var folder = await CreateFolder(
            workspace: workspace,
            user: AppOwner);

        var uploadedFile = await UploadFile(
            content: Random.Bytes(256),
            fileName: $"{Random.Name("file")}.bin",
            contentType: "application/octet-stream",
            folder: folder,
            workspace: workspace,
            user: AppOwner);

        // Fresh user — registered via invitation, but never configured an encryption password.
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

        //when — invitee hits a full-encryption endpoint with no encryption session
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.Files.GetDownloadLink(
                workspaceExternalId: workspace.ExternalId,
                fileExternalId: uploadedFile.ExternalId,
                contentDisposition: "attachment",
                cookie: invitee.Cookie,
                workspaceEncryptionSession: null));

        //then — server distinguishes "no setup yet" from "setup but not unlocked",
        //       so the UI can open the setup dialog instead of the unlock dialog.
        apiError.StatusCode.Should().Be(StatusCodes.Status423Locked);
        apiError.HttpError.Should().NotBeNull();
        apiError.HttpError!.Code.Should().Be("user-encryption-setup-required");
    }

    [Fact]
    public async Task after_invitee_sets_up_encryption_access_switches_from_setup_required_to_pending_key_grant()
    {
        //given — same full-encrypted workspace with a file, fresh invited user without encryption
        var storage = await CreateHardDriveStorage(
            user: AppOwner,
            encryptionType: StorageEncryptionType.Full);

        var workspace = await CreateWorkspace(
            storage: storage,
            user: AppOwner);

        var folder = await CreateFolder(
            workspace: workspace,
            user: AppOwner);

        var uploadedFile = await UploadFile(
            content: Random.Bytes(256),
            fileName: $"{Random.Name("file")}.bin",
            contentType: "application/octet-stream",
            folder: folder,
            workspace: workspace,
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

        //when — invitee configures their own encryption password; server now has
        //       their public key but still no wek wrap for this workspace.
        var inviteeSetup = await Api.UserEncryptionPassword.Setup(
            userExternalId: invitee.ExternalId,
            encryptionPassword: "Invitee-Encryption-Password-1!",
            cookie: invitee.Cookie,
            antiforgery: invitee.Antiforgery);

        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.Files.GetDownloadLink(
                workspaceExternalId: workspace.ExternalId,
                fileExternalId: uploadedFile.ExternalId,
                contentDisposition: "attachment",
                cookie: invitee.Cookie,
                workspaceEncryptionSession: inviteeSetup.EncryptionCookie));

        //then — no longer setup-required nor session-required; the Phase 2 flow
        //       takes over and asks the invitee to wait for owner approval.
        apiError.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        apiError.HttpError.Should().NotBeNull();
        apiError.HttpError!.Code.Should().Be("workspace-encryption-pending-key-grant");
    }

    [Fact]
    public async Task after_lock_and_unlock_full_workspace_access_is_restored()
    {
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

        await Api.UserEncryptionPassword.Lock(
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //when
        var encryptionCookie = await Api.UserEncryptionPassword.Unlock(
            userExternalId: AppOwner.ExternalId,
            encryptionPassword: DefaultTestEncryptionPassword,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        var workspaceAfterUnlock = workspace with { WorkspaceEncryptionSession = encryptionCookie };

        var downloaded = await DownloadFile(
            fileExternalId: uploadedFile.ExternalId,
            workspace: workspaceAfterUnlock,
            user: AppOwner);

        //then
        downloaded.Should().Equal(content);
    }
}
