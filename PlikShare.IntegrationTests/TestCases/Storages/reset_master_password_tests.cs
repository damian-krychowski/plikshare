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
using PlikShare.Storages.Id;
using Xunit.Abstractions;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.IntegrationTests.TestCases.Storages;

[Collection(IntegrationTestsCollection.Name)]
public class reset_master_password_tests : TestFixture
{
    private AppSignedInUser AppOwner { get; }

    public reset_master_password_tests(
        HostFixture8081 hostFixture,
        ITestOutputHelper testOutputHelper) : base(hostFixture, testOutputHelper)
    {
        AppOwner = SignIn(user: Users.AppOwner).Result;
    }

    private async Task<(StorageExtId StorageExternalId, string RecoveryCode, string MasterPassword)>
        CreateFullEncryptionStorage(string masterPassword)
    {
        var storageName = Random.Name("hard-drive");

        var response = await Api.Storages.CreateHardDriveStorage(
            request: new CreateHardDriveStorageRequestDto(
                Name: storageName,
                VolumePath: MainVolume.Path,
                FolderPath: $"/{storageName}",
                EncryptionType: StorageEncryptionType.Full,
                MasterPassword: masterPassword),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        return (response.ExternalId, response.RecoveryCode!, masterPassword);
    }

    [Fact]
    public async Task when_master_password_is_reset_with_valid_recovery_code_it_succeeds()
    {
        //given
        var (storageId, recoveryCode, _) = await CreateFullEncryptionStorage("Original-1!");

        //when
        await Api.Storages.ResetMasterPassword(
            externalId: storageId,
            request: new ResetMasterPasswordRequestDto(
                RecoveryCode: recoveryCode,
                NewPassword: "After-Reset-1!"),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then — the new password works for unlock
        var sessionCookie = await Api.Storages.UnlockFullEncryption(
            externalId: storageId,
            request: new UnlockFullEncryptionRequestDto(MasterPassword: "After-Reset-1!"),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        sessionCookie.Value.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task when_master_password_is_reset_with_wrong_word_count_it_fails_with_malformed()
    {
        //given
        var (storageId, recoveryCode, _) = await CreateFullEncryptionStorage("Original-1!");

        // Take first 23 words of the valid code — too short.
        var truncated = string.Join(' ',
            recoveryCode.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(23));

        //when
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.Storages.ResetMasterPassword(
                externalId: storageId,
                request: new ResetMasterPasswordRequestDto(
                    RecoveryCode: truncated,
                    NewPassword: "New-1!"),
                cookie: AppOwner.Cookie,
                antiforgery: AppOwner.Antiforgery));

        //then
        apiError.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        apiError.HttpError!.Code.Should().Be("malformed-recovery-code");
    }

    [Fact]
    public async Task when_master_password_is_reset_with_unknown_word_it_fails_with_malformed()
    {
        //given
        var (storageId, recoveryCode, _) = await CreateFullEncryptionStorage("Original-1!");

        // Replace the last word with a non-BIP-39 word.
        var words = recoveryCode.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToArray();
        words[^1] = "notabip39word";
        var tampered = string.Join(' ', words);

        //when
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.Storages.ResetMasterPassword(
                externalId: storageId,
                request: new ResetMasterPasswordRequestDto(
                    RecoveryCode: tampered,
                    NewPassword: "New-1!"),
                cookie: AppOwner.Cookie,
                antiforgery: AppOwner.Antiforgery));

        //then
        apiError.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        apiError.HttpError!.Code.Should().Be("malformed-recovery-code");
    }

    [Fact]
    public async Task when_master_password_is_reset_with_bad_checksum_it_fails_with_malformed()
    {
        //given
        var (storageId, recoveryCode, _) = await CreateFullEncryptionStorage("Original-1!");

        // 24 all-'abandon' words form a valid BIP-39 token pattern but do not match
        // this storage's random seed — checksum validation catches the mismatch.
        var badChecksum = string.Join(' ', Enumerable.Repeat("abandon", 23).Append("ability"));

        //when
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.Storages.ResetMasterPassword(
                externalId: storageId,
                request: new ResetMasterPasswordRequestDto(
                    RecoveryCode: badChecksum,
                    NewPassword: "New-1!"),
                cookie: AppOwner.Cookie,
                antiforgery: AppOwner.Antiforgery));

        //then
        apiError.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        apiError.HttpError!.Code.Should().Be("malformed-recovery-code");
    }

    [Fact]
    public async Task when_master_password_is_reset_with_empty_recovery_code_it_fails_with_malformed()
    {
        //given
        var (storageId, _, _) = await CreateFullEncryptionStorage("Original-1!");

        //when
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.Storages.ResetMasterPassword(
                externalId: storageId,
                request: new ResetMasterPasswordRequestDto(
                    RecoveryCode: "",
                    NewPassword: "New-1!"),
                cookie: AppOwner.Cookie,
                antiforgery: AppOwner.Antiforgery));

        //then
        apiError.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        apiError.HttpError!.Code.Should().Be("malformed-recovery-code");
    }

    [Fact]
    public async Task when_master_password_is_reset_with_another_storages_valid_recovery_code_it_fails_with_invalid()
    {
        //given — create two storages; use B's recovery code to try resetting A
        var (storageA, _, _) = await CreateFullEncryptionStorage("Password-A-1!");
        var (_, recoveryCodeB, _) = await CreateFullEncryptionStorage("Password-B-1!");

        //when
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.Storages.ResetMasterPassword(
                externalId: storageA,
                request: new ResetMasterPasswordRequestDto(
                    RecoveryCode: recoveryCodeB,
                    NewPassword: "New-1!"),
                cookie: AppOwner.Cookie,
                antiforgery: AppOwner.Antiforgery));

        //then — code decodes fine but RecoveryVerifyHash does not match storage A
        apiError.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        apiError.HttpError!.Code.Should().Be("invalid-recovery-code");
    }

    [Fact]
    public async Task after_reset_files_uploaded_before_are_still_decryptable()
    {
        //given — this is the critical disaster-recovery invariant:
        // DEK is deterministically derived from the recovery seed, so reset must
        // reproduce the same DEK that was used to encrypt the original files.
        var originalPassword = "Original-Password-1!";
        var resetPassword = "Post-Reset-Password-1!";

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

        var recoveryCode = storageResponse.RecoveryCode!;

        var originalSessionCookie = await Api.Storages.UnlockFullEncryption(
            externalId: storageResponse.ExternalId,
            request: new UnlockFullEncryptionRequestDto(MasterPassword: originalPassword),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        var storage = new AppStorage(
            ExternalId: storageResponse.ExternalId,
            Name: storageName,
            Type: StorageType.HardDrive,
            Details: null,
            FullEncryptionSession: originalSessionCookie);

        var workspace = await CreateWorkspace(storage, AppOwner);
        var folder = await CreateFolder(parent: null, workspace, AppOwner);

        var originalContent = Encoding.UTF8.GetBytes(
            "Content encrypted under the original DEK — must survive password reset.");

        var uploaded = await UploadFile(
            content: originalContent,
            fileName: "pre-reset-file.txt",
            contentType: "text/plain",
            folder: folder,
            workspace: workspace,
            user: AppOwner);

        //when — reset password using recovery code, then unlock with new password
        await Api.Storages.ResetMasterPassword(
            externalId: storageResponse.ExternalId,
            request: new ResetMasterPasswordRequestDto(
                RecoveryCode: recoveryCode,
                NewPassword: resetPassword),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        var postResetSessionCookie = await Api.Storages.UnlockFullEncryption(
            externalId: storageResponse.ExternalId,
            request: new UnlockFullEncryptionRequestDto(MasterPassword: resetPassword),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        var workspaceAfterReset = workspace with { FullEncryptionSession = postResetSessionCookie };

        var downloaded = await DownloadFile(
            fileExternalId: uploaded.ExternalId,
            workspace: workspaceAfterReset,
            user: AppOwner);

        //then
        downloaded.Should().BeEquivalentTo(originalContent);
    }

    [Fact]
    public async Task after_reset_old_password_no_longer_works_for_unlock()
    {
        //given
        var originalPassword = "Original-Password-1!";
        var (storageId, recoveryCode, _) = await CreateFullEncryptionStorage(originalPassword);

        //when
        await Api.Storages.ResetMasterPassword(
            externalId: storageId,
            request: new ResetMasterPasswordRequestDto(
                RecoveryCode: recoveryCode,
                NewPassword: "After-Reset-1!"),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.Storages.UnlockFullEncryption(
                externalId: storageId,
                request: new UnlockFullEncryptionRequestDto(MasterPassword: originalPassword),
                cookie: AppOwner.Cookie,
                antiforgery: AppOwner.Antiforgery));

        apiError.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        apiError.HttpError!.Code.Should().Be("invalid-master-password");
    }

    [Fact]
    public async Task after_reset_recovery_code_still_works_for_subsequent_reset()
    {
        //given — recovery code is permanent; each reset re-binds the DEK wrap
        // but RecoveryVerifyHash is preserved.
        var (storageId, recoveryCode, _) = await CreateFullEncryptionStorage("Original-1!");

        await Api.Storages.ResetMasterPassword(
            externalId: storageId,
            request: new ResetMasterPasswordRequestDto(
                RecoveryCode: recoveryCode,
                NewPassword: "First-Reset-1!"),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //when — the same recovery code is used again
        await Api.Storages.ResetMasterPassword(
            externalId: storageId,
            request: new ResetMasterPasswordRequestDto(
                RecoveryCode: recoveryCode,
                NewPassword: "Second-Reset-1!"),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var sessionCookie = await Api.Storages.UnlockFullEncryption(
            externalId: storageId,
            request: new UnlockFullEncryptionRequestDto(MasterPassword: "Second-Reset-1!"),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        sessionCookie.Value.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task when_master_password_is_reset_on_none_encryption_storage_it_fails_with_mode_mismatch()
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
            async () => await Api.Storages.ResetMasterPassword(
                externalId: storageResponse.ExternalId,
                request: new ResetMasterPasswordRequestDto(
                    RecoveryCode: "any",
                    NewPassword: "New-1!"),
                cookie: AppOwner.Cookie,
                antiforgery: AppOwner.Antiforgery));

        //then
        apiError.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        apiError.HttpError!.Code.Should().Be("storage-encryption-mode-mismatch");
    }

    [Fact]
    public async Task when_master_password_is_reset_on_managed_encryption_storage_it_fails_with_mode_mismatch()
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
            async () => await Api.Storages.ResetMasterPassword(
                externalId: storageResponse.ExternalId,
                request: new ResetMasterPasswordRequestDto(
                    RecoveryCode: "any",
                    NewPassword: "New-1!"),
                cookie: AppOwner.Cookie,
                antiforgery: AppOwner.Antiforgery));

        //then
        apiError.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        apiError.HttpError!.Code.Should().Be("storage-encryption-mode-mismatch");
    }

    [Fact]
    public async Task when_master_password_is_reset_on_nonexistent_storage_it_fails_with_not_found()
    {
        //given
        var nonexistentStorageId = StorageExtId.NewId();

        //when
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.Storages.ResetMasterPassword(
                externalId: nonexistentStorageId,
                request: new ResetMasterPasswordRequestDto(
                    RecoveryCode: "any",
                    NewPassword: "New-1!"),
                cookie: AppOwner.Cookie,
                antiforgery: AppOwner.Antiforgery));

        //then
        apiError.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task resetting_master_password_should_produce_audit_log_entry()
    {
        //given
        var (storageId, recoveryCode, _) = await CreateFullEncryptionStorage("Original-1!");

        //when
        await Api.Storages.ResetMasterPassword(
            externalId: storageId,
            request: new ResetMasterPasswordRequestDto(
                RecoveryCode: recoveryCode,
                NewPassword: "After-Reset-1!"),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<Audit.Storage.MasterPasswordReset>(
            expectedEventType: AuditLogEventTypes.Storage.MasterPasswordReset,
            assertDetails: details =>
                details.Storage.ExternalId.Should().Be(storageId),
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Warning);
    }

    [Fact]
    public async Task failed_reset_with_malformed_code_should_produce_audit_log_entry()
    {
        //given
        var (storageId, _, _) = await CreateFullEncryptionStorage("Original-1!");

        //when
        await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.Storages.ResetMasterPassword(
                externalId: storageId,
                request: new ResetMasterPasswordRequestDto(
                    RecoveryCode: "only two words",
                    NewPassword: "New-1!"),
                cookie: AppOwner.Cookie,
                antiforgery: AppOwner.Antiforgery));

        //then
        await AssertAuditLogContains<Audit.Storage.MasterPasswordResetFailed>(
            expectedEventType: AuditLogEventTypes.Storage.MasterPasswordResetFailed,
            assertDetails: details =>
            {
                details.Storage.ExternalId.Should().Be(storageId);
                details.Reason.Should().Be("malformed-recovery-code");
            },
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Warning);
    }

    [Fact]
    public async Task failed_reset_with_invalid_code_should_produce_audit_log_entry()
    {
        //given
        var (storageA, _, _) = await CreateFullEncryptionStorage("Password-A-1!");
        var (_, recoveryCodeB, _) = await CreateFullEncryptionStorage("Password-B-1!");

        //when
        await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.Storages.ResetMasterPassword(
                externalId: storageA,
                request: new ResetMasterPasswordRequestDto(
                    RecoveryCode: recoveryCodeB,
                    NewPassword: "New-1!"),
                cookie: AppOwner.Cookie,
                antiforgery: AppOwner.Antiforgery));

        //then
        await AssertAuditLogContains<Audit.Storage.MasterPasswordResetFailed>(
            expectedEventType: AuditLogEventTypes.Storage.MasterPasswordResetFailed,
            assertDetails: details =>
            {
                details.Storage.ExternalId.Should().Be(storageA);
                details.Reason.Should().Be("invalid-recovery-code");
            },
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Warning);
    }

    [Fact]
    public async Task failed_reset_on_mode_mismatch_should_produce_audit_log_entry()
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
            async () => await Api.Storages.ResetMasterPassword(
                externalId: storageResponse.ExternalId,
                request: new ResetMasterPasswordRequestDto(
                    RecoveryCode: "any",
                    NewPassword: "New-1!"),
                cookie: AppOwner.Cookie,
                antiforgery: AppOwner.Antiforgery));

        //then
        await AssertAuditLogContains<Audit.Storage.MasterPasswordResetFailed>(
            expectedEventType: AuditLogEventTypes.Storage.MasterPasswordResetFailed,
            assertDetails: details =>
            {
                details.Storage.ExternalId.Should().Be(storageResponse.ExternalId);
                details.Reason.Should().Be("encryption-mode-mismatch");
            },
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Warning);
    }
}
