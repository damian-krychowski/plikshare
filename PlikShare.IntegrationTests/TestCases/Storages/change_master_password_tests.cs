using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using PlikShare.AuditLog;
using PlikShare.AuditLog.Details;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.Apis;
using PlikShare.Storages;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.Entities;
using PlikShare.Storages.HardDrive.Create.Contracts;
using Xunit.Abstractions;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.IntegrationTests.TestCases.Storages;

[Collection(IntegrationTestsCollection.Name)]
public class change_master_password_tests : TestFixture
{
    private AppSignedInUser AppOwner { get; }

    public change_master_password_tests(
        HostFixture8081 hostFixture,
        ITestOutputHelper testOutputHelper) : base(hostFixture, testOutputHelper)
    {
        AppOwner = SignIn(user: Users.AppOwner).Result;
    }

    [Fact]
    public async Task when_master_password_is_changed_with_correct_old_password_it_succeeds_and_issues_new_cookie()
    {
        //given
        var oldPassword = "Old-Master-Password-1!";
        var newPassword = "New-Master-Password-1!";
        var storage = await CreateHardDriveStorage(
            user: AppOwner,
            encryptionType: StorageEncryptionType.Full);

        // Re-create storage with explicit password so we know it
        var storageName = Random.Name("hard-drive");
        var storageResponse = await Api.Storages.CreateHardDriveStorage(
            request: new CreateHardDriveStorageRequestDto(
                Name: storageName,
                VolumePath: MainVolume.Path,
                FolderPath: $"/{storageName}",
                EncryptionType: StorageEncryptionType.Full,
                MasterPassword: oldPassword),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //when
        var newSessionCookie = await Api.Storages.ChangeMasterPassword(
            externalId: storageResponse.ExternalId,
            request: new ChangeMasterPasswordRequestDto(
                OldPassword: oldPassword,
                NewPassword: newPassword),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        newSessionCookie.Name.Should().Be(FullEncryptionSessionCookie.GetCookieName(storageResponse.ExternalId));
        newSessionCookie.Value.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task when_master_password_is_changed_with_wrong_old_password_it_fails()
    {
        //given
        var storageName = Random.Name("hard-drive");
        var storageResponse = await Api.Storages.CreateHardDriveStorage(
            request: new CreateHardDriveStorageRequestDto(
                Name: storageName,
                VolumePath: MainVolume.Path,
                FolderPath: $"/{storageName}",
                EncryptionType: StorageEncryptionType.Full,
                MasterPassword: "Correct-Old-1!"),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //when
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.Storages.ChangeMasterPassword(
                externalId: storageResponse.ExternalId,
                request: new ChangeMasterPasswordRequestDto(
                    OldPassword: "Wrong-Old-1!",
                    NewPassword: "New-Password-1!"),
                cookie: AppOwner.Cookie,
                antiforgery: AppOwner.Antiforgery));

        //then
        apiError.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        apiError.HttpError.Should().NotBeNull();
        apiError.HttpError!.Code.Should().Be("invalid-master-password");
    }

    [Fact]
    public async Task when_master_password_is_changed_on_none_encryption_storage_it_fails_with_mode_mismatch()
    {
        //given
        var storageName = Random.Name("hard-drive");
        var storageResponse = await Api.Storages.CreateHardDriveStorage(
            request: new CreateHardDriveStorageRequestDto(
                Name: storageName,
                VolumePath: MainVolume.Path,
                FolderPath: $"/{storageName}",
                EncryptionType: StorageEncryptionType.None),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //when
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.Storages.ChangeMasterPassword(
                externalId: storageResponse.ExternalId,
                request: new ChangeMasterPasswordRequestDto(
                    OldPassword: "any",
                    NewPassword: "New-Password-1!"),
                cookie: AppOwner.Cookie,
                antiforgery: AppOwner.Antiforgery));

        //then
        apiError.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        apiError.HttpError.Should().NotBeNull();
        apiError.HttpError!.Code.Should().Be("storage-encryption-mode-mismatch");
    }

    [Fact]
    public async Task when_master_password_is_changed_on_managed_encryption_storage_it_fails_with_mode_mismatch()
    {
        //given
        var storageName = Random.Name("hard-drive");
        var storageResponse = await Api.Storages.CreateHardDriveStorage(
            request: new CreateHardDriveStorageRequestDto(
                Name: storageName,
                VolumePath: MainVolume.Path,
                FolderPath: $"/{storageName}",
                EncryptionType: StorageEncryptionType.Managed),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //when
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.Storages.ChangeMasterPassword(
                externalId: storageResponse.ExternalId,
                request: new ChangeMasterPasswordRequestDto(
                    OldPassword: "any",
                    NewPassword: "New-Password-1!"),
                cookie: AppOwner.Cookie,
                antiforgery: AppOwner.Antiforgery));

        //then
        apiError.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        apiError.HttpError.Should().NotBeNull();
        apiError.HttpError!.Code.Should().Be("storage-encryption-mode-mismatch");
    }

    [Fact]
    public async Task after_change_files_uploaded_before_are_still_decryptable_with_new_cookie()
    {
        //given
        var oldPassword = "Old-Password-1!";
        var newPassword = "New-Password-1!";

        var storageName = Random.Name("hard-drive");
        var storageResponse = await Api.Storages.CreateHardDriveStorage(
            request: new CreateHardDriveStorageRequestDto(
                Name: storageName,
                VolumePath: MainVolume.Path,
                FolderPath: $"/{storageName}",
                EncryptionType: StorageEncryptionType.Full,
                MasterPassword: oldPassword),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        var oldSessionCookie = await Api.Storages.UnlockFullEncryption(
            externalId: storageResponse.ExternalId,
            request: new UnlockFullEncryptionRequestDto(MasterPassword: oldPassword),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        var storage = new AppStorage(
            ExternalId: storageResponse.ExternalId,
            Name: storageName,
            Type: StorageType.HardDrive,
            Details: null,
            FullEncryptionSession: oldSessionCookie);

        var workspace = await CreateWorkspace(storage, AppOwner);
        var folder = await CreateFolder(parent: null, workspace, AppOwner);

        var originalContent = Encoding.UTF8.GetBytes(
            "Content encrypted under the original master password.");

        var uploaded = await UploadFile(
            content: originalContent,
            fileName: "before-change.txt",
            contentType: "text/plain",
            folder: folder,
            workspace: workspace,
            user: AppOwner);

        //when — change password, receive new cookie, build new workspace view
        var newSessionCookie = await Api.Storages.ChangeMasterPassword(
            externalId: storageResponse.ExternalId,
            request: new ChangeMasterPasswordRequestDto(
                OldPassword: oldPassword,
                NewPassword: newPassword),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        var workspaceWithNewSession = workspace with { FullEncryptionSession = newSessionCookie };

        var downloaded = await DownloadFile(
            fileExternalId: uploaded.ExternalId,
            workspace: workspaceWithNewSession,
            user: AppOwner);

        //then — DEK was rewrapped under new KEK, so file still decrypts
        downloaded.Should().BeEquivalentTo(originalContent);
    }

    [Fact]
    public async Task after_change_file_uploaded_before_and_after_change_are_both_decryptable()
    {
        //given — this verifies that the rewrapped DEK is stable across uploads:
        // files uploaded before the password change use the same DEK as files
        // uploaded after the change, just wrapped under a different KEK.
        var oldPassword = "Old-Password-1!";
        var newPassword = "New-Password-1!";

        var storageName = Random.Name("hard-drive");
        var storageResponse = await Api.Storages.CreateHardDriveStorage(
            request: new CreateHardDriveStorageRequestDto(
                Name: storageName,
                VolumePath: MainVolume.Path,
                FolderPath: $"/{storageName}",
                EncryptionType: StorageEncryptionType.Full,
                MasterPassword: oldPassword),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        var oldSessionCookie = await Api.Storages.UnlockFullEncryption(
            externalId: storageResponse.ExternalId,
            request: new UnlockFullEncryptionRequestDto(MasterPassword: oldPassword),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        var storage = new AppStorage(
            ExternalId: storageResponse.ExternalId,
            Name: storageName,
            Type: StorageType.HardDrive,
            Details: null,
            FullEncryptionSession: oldSessionCookie);

        var workspace = await CreateWorkspace(storage, AppOwner);
        var folder = await CreateFolder(parent: null, workspace, AppOwner);

        var contentA = Encoding.UTF8.GetBytes("File uploaded BEFORE password change.");
        var fileA = await UploadFile(
            content: contentA,
            fileName: "before.txt",
            contentType: "text/plain",
            folder: folder,
            workspace: workspace,
            user: AppOwner);

        //when — change password, then upload a second file under the new session
        var newSessionCookie = await Api.Storages.ChangeMasterPassword(
            externalId: storageResponse.ExternalId,
            request: new ChangeMasterPasswordRequestDto(
                OldPassword: oldPassword,
                NewPassword: newPassword),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        var workspaceWithNewSession = workspace with { FullEncryptionSession = newSessionCookie };

        var contentB = Encoding.UTF8.GetBytes("File uploaded AFTER password change.");
        var fileB = await UploadFile(
            content: contentB,
            fileName: "after.txt",
            contentType: "text/plain",
            folder: folder,
            workspace: workspaceWithNewSession,
            user: AppOwner);

        //then — both files decrypt correctly when read with the new session cookie
        var downloadedA = await DownloadFile(
            fileExternalId: fileA.ExternalId,
            workspace: workspaceWithNewSession,
            user: AppOwner);

        var downloadedB = await DownloadFile(
            fileExternalId: fileB.ExternalId,
            workspace: workspaceWithNewSession,
            user: AppOwner);

        downloadedA.Should().BeEquivalentTo(contentA);
        downloadedB.Should().BeEquivalentTo(contentB);
    }

    [Fact]
    public async Task after_change_old_password_no_longer_works_for_unlock()
    {
        //given
        var oldPassword = "Old-Password-1!";
        var newPassword = "New-Password-1!";

        var storageName = Random.Name("hard-drive");
        var storageResponse = await Api.Storages.CreateHardDriveStorage(
            request: new CreateHardDriveStorageRequestDto(
                Name: storageName,
                VolumePath: MainVolume.Path,
                FolderPath: $"/{storageName}",
                EncryptionType: StorageEncryptionType.Full,
                MasterPassword: oldPassword),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //when
        await Api.Storages.ChangeMasterPassword(
            externalId: storageResponse.ExternalId,
            request: new ChangeMasterPasswordRequestDto(
                OldPassword: oldPassword,
                NewPassword: newPassword),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then — old password no longer authenticates
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.Storages.UnlockFullEncryption(
                externalId: storageResponse.ExternalId,
                request: new UnlockFullEncryptionRequestDto(MasterPassword: oldPassword),
                cookie: AppOwner.Cookie,
                antiforgery: AppOwner.Antiforgery));

        apiError.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        apiError.HttpError!.Code.Should().Be("invalid-master-password");
    }

    [Fact]
    public async Task after_change_new_password_works_for_unlock()
    {
        //given
        var oldPassword = "Old-Password-1!";
        var newPassword = "New-Password-1!";

        var storageName = Random.Name("hard-drive");
        var storageResponse = await Api.Storages.CreateHardDriveStorage(
            request: new CreateHardDriveStorageRequestDto(
                Name: storageName,
                VolumePath: MainVolume.Path,
                FolderPath: $"/{storageName}",
                EncryptionType: StorageEncryptionType.Full,
                MasterPassword: oldPassword),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //when
        await Api.Storages.ChangeMasterPassword(
            externalId: storageResponse.ExternalId,
            request: new ChangeMasterPasswordRequestDto(
                OldPassword: oldPassword,
                NewPassword: newPassword),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then — new password unlocks
        var sessionCookie = await Api.Storages.UnlockFullEncryption(
            externalId: storageResponse.ExternalId,
            request: new UnlockFullEncryptionRequestDto(MasterPassword: newPassword),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        sessionCookie.Name.Should().Be(FullEncryptionSessionCookie.GetCookieName(storageResponse.ExternalId));
        sessionCookie.Value.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task after_change_recovery_code_from_creation_still_works_for_reset()
    {
        //given
        var originalPassword = "Original-Password-1!";
        var intermediatePassword = "Intermediate-Password-1!";
        var resetPassword = "Reset-Password-1!";

        var storageName = Random.Name("hard-drive");
        var storageResponse = await Api.Storages.CreateHardDriveStorage(
            request: new CreateHardDriveStorageRequestDto(
                Name: storageName,
                VolumePath: MainVolume.Path,
                FolderPath: $"/{storageName}",
                EncryptionType: StorageEncryptionType.Full,
                MasterPassword: originalPassword),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        var originalRecoveryCode = storageResponse.RecoveryCode!;

        // change password — this preserves RecoveryVerifyHash invariant
        await Api.Storages.ChangeMasterPassword(
            externalId: storageResponse.ExternalId,
            request: new ChangeMasterPasswordRequestDto(
                OldPassword: originalPassword,
                NewPassword: intermediatePassword),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //when — original recovery code should still reset the password
        await Api.Storages.ResetMasterPassword(
            externalId: storageResponse.ExternalId,
            request: new ResetMasterPasswordRequestDto(
                RecoveryCode: originalRecoveryCode,
                NewPassword: resetPassword),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then — the post-reset password unlocks the storage
        var sessionCookie = await Api.Storages.UnlockFullEncryption(
            externalId: storageResponse.ExternalId,
            request: new UnlockFullEncryptionRequestDto(MasterPassword: resetPassword),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        sessionCookie.Value.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task changing_master_password_should_produce_audit_log_entry()
    {
        //given
        var oldPassword = "Old-Password-1!";
        var newPassword = "New-Password-1!";

        var storageName = Random.Name("hard-drive");
        var storageResponse = await Api.Storages.CreateHardDriveStorage(
            request: new CreateHardDriveStorageRequestDto(
                Name: storageName,
                VolumePath: MainVolume.Path,
                FolderPath: $"/{storageName}",
                EncryptionType: StorageEncryptionType.Full,
                MasterPassword: oldPassword),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //when
        await Api.Storages.ChangeMasterPassword(
            externalId: storageResponse.ExternalId,
            request: new ChangeMasterPasswordRequestDto(
                OldPassword: oldPassword,
                NewPassword: newPassword),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<Audit.Storage.MasterPasswordChanged>(
            expectedEventType: AuditLogEventTypes.Storage.MasterPasswordChanged,
            assertDetails: details =>
                details.Storage.ExternalId.Should().Be(storageResponse.ExternalId),
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Info);
    }

    [Fact]
    public async Task failed_change_with_wrong_old_password_should_produce_audit_log_entry()
    {
        //given
        var storageName = Random.Name("hard-drive");
        var storageResponse = await Api.Storages.CreateHardDriveStorage(
            request: new CreateHardDriveStorageRequestDto(
                Name: storageName,
                VolumePath: MainVolume.Path,
                FolderPath: $"/{storageName}",
                EncryptionType: StorageEncryptionType.Full,
                MasterPassword: "Correct-1!"),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //when
        await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.Storages.ChangeMasterPassword(
                externalId: storageResponse.ExternalId,
                request: new ChangeMasterPasswordRequestDto(
                    OldPassword: "Wrong-1!",
                    NewPassword: "New-1!"),
                cookie: AppOwner.Cookie,
                antiforgery: AppOwner.Antiforgery));

        //then
        await AssertAuditLogContains<Audit.Storage.MasterPasswordChangeFailed>(
            expectedEventType: AuditLogEventTypes.Storage.MasterPasswordChangeFailed,
            assertDetails: details =>
            {
                details.Storage.ExternalId.Should().Be(storageResponse.ExternalId);
                details.Reason.Should().Be("invalid-old-password");
            },
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Warning);
    }

    [Fact]
    public async Task failed_change_on_mode_mismatch_should_produce_audit_log_entry()
    {
        //given
        var storageName = Random.Name("hard-drive");
        var storageResponse = await Api.Storages.CreateHardDriveStorage(
            request: new CreateHardDriveStorageRequestDto(
                Name: storageName,
                VolumePath: MainVolume.Path,
                FolderPath: $"/{storageName}",
                EncryptionType: StorageEncryptionType.Managed),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //when
        await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.Storages.ChangeMasterPassword(
                externalId: storageResponse.ExternalId,
                request: new ChangeMasterPasswordRequestDto(
                    OldPassword: "any",
                    NewPassword: "New-1!"),
                cookie: AppOwner.Cookie,
                antiforgery: AppOwner.Antiforgery));

        //then
        await AssertAuditLogContains<Audit.Storage.MasterPasswordChangeFailed>(
            expectedEventType: AuditLogEventTypes.Storage.MasterPasswordChangeFailed,
            assertDetails: details =>
            {
                details.Storage.ExternalId.Should().Be(storageResponse.ExternalId);
                details.Reason.Should().Be("encryption-mode-mismatch");
            },
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Warning);
    }
}
