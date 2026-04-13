using FluentAssertions;
using Microsoft.AspNetCore.Http;
using PlikShare.Core.Utils;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.Apis;
using PlikShare.Storages;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.HardDrive.Create.Contracts;
using PlikShare.Storages.Id;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Storages;

[Collection(IntegrationTestsCollection.Name)]
public class unlock_full_encryption_tests : TestFixture
{
    private AppSignedInUser AppOwner { get; }

    public unlock_full_encryption_tests(
        HostFixture8081 hostFixture,
        ITestOutputHelper testOutputHelper) : base(hostFixture, testOutputHelper)
    {
        AppOwner = SignIn(user: Users.AppOwner).Result;
    }

    private async Task<StorageExtId> CreateFullEncryptionStorage(
        string masterPassword)
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

        return response.ExternalId;
    }

    [Fact]
    public async Task when_full_encryption_storage_is_unlocked_with_correct_password_session_cookie_is_issued()
    {
        //given
        var masterPassword = "Correct-Master-Password-1!";
        var storageExternalId = await CreateFullEncryptionStorage(masterPassword);

        //when
        var sessionCookie = await Api.Storages.UnlockFullEncryption(
            externalId: storageExternalId,
            request: new UnlockFullEncryptionRequestDto(MasterPassword: masterPassword),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        sessionCookie.Name.Should().Be(FullEncryptionSessionCookie.GetCookieName(storageExternalId));
        sessionCookie.Value.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task when_full_encryption_storage_is_unlocked_with_wrong_password_it_fails_with_invalid_master_password()
    {
        //given
        var storageExternalId = await CreateFullEncryptionStorage("Correct-Master-Password-1!");

        //when
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.Storages.UnlockFullEncryption(
                externalId: storageExternalId,
                request: new UnlockFullEncryptionRequestDto(MasterPassword: "Wrong-Master-Password-1!"),
                cookie: AppOwner.Cookie,
                antiforgery: AppOwner.Antiforgery));

        //then
        apiError.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        apiError.HttpError.Should().NotBeNull();
        apiError.HttpError!.Code.Should().Be("invalid-master-password");
    }

    [Fact]
    public async Task when_none_encryption_storage_is_unlocked_it_fails_with_encryption_mode_mismatch()
    {
        //given
        var storageName = Random.Name("hard-drive");

        var response = await Api.Storages.CreateHardDriveStorage(
            request: new CreateHardDriveStorageRequestDto(
                Name: storageName,
                VolumePath: MainVolume.Path,
                FolderPath: $"/{storageName}",
                EncryptionType: StorageEncryptionType.None),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //when
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.Storages.UnlockFullEncryption(
                externalId: response.ExternalId,
                request: new UnlockFullEncryptionRequestDto(MasterPassword: "any-password"),
                cookie: AppOwner.Cookie,
                antiforgery: AppOwner.Antiforgery));

        //then
        apiError.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        apiError.HttpError.Should().NotBeNull();
        apiError.HttpError!.Code.Should().Be("storage-encryption-mode-mismatch");
    }

    [Fact]
    public async Task when_managed_encryption_storage_is_unlocked_it_fails_with_encryption_mode_mismatch()
    {
        //given
        var storageName = Random.Name("hard-drive");

        var response = await Api.Storages.CreateHardDriveStorage(
            request: new CreateHardDriveStorageRequestDto(
                Name: storageName,
                VolumePath: MainVolume.Path,
                FolderPath: $"/{storageName}",
                EncryptionType: StorageEncryptionType.Managed),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //when
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.Storages.UnlockFullEncryption(
                externalId: response.ExternalId,
                request: new UnlockFullEncryptionRequestDto(MasterPassword: "any-password"),
                cookie: AppOwner.Cookie,
                antiforgery: AppOwner.Antiforgery));

        //then
        apiError.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        apiError.HttpError.Should().NotBeNull();
        apiError.HttpError!.Code.Should().Be("storage-encryption-mode-mismatch");
    }

    [Fact]
    public async Task when_nonexistent_storage_is_unlocked_it_fails_with_not_found()
    {
        //given
        var nonexistentStorageId = StorageExtId.NewId();

        //when
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.Storages.UnlockFullEncryption(
                externalId: nonexistentStorageId,
                request: new UnlockFullEncryptionRequestDto(MasterPassword: "any-password"),
                cookie: AppOwner.Cookie,
                antiforgery: AppOwner.Antiforgery));

        //then
        apiError.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task unlocking_storage_a_does_not_unlock_storage_b()
    {
        //given
        var storageAId = await CreateFullEncryptionStorage("Password-A-1!");
        var storageBId = await CreateFullEncryptionStorage("Password-B-1!");

        //when — unlock only A
        var sessionA = await Api.Storages.UnlockFullEncryption(
            externalId: storageAId,
            request: new UnlockFullEncryptionRequestDto(MasterPassword: "Password-A-1!"),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then — A's cookie name targets only A
        sessionA.Name.Should().Be(FullEncryptionSessionCookie.GetCookieName(storageAId));
        sessionA.Name.Should().NotBe(FullEncryptionSessionCookie.GetCookieName(storageBId));

        // and — listing sessions shows only A
        var sessions = await Api.FullEncryptionSessions.Get(
            cookie: AppOwner.Cookie,
            fullEncryptionSessions: sessionA);

        sessions.Items.Should().HaveCount(1);
        sessions.Items.Should().Contain(i => i.StorageExternalId == storageAId);
        sessions.Items.Should().NotContain(i => i.StorageExternalId == storageBId);
    }

    [Fact]
    public async Task when_invited_user_unlocks_storage_they_get_their_own_session_cookie()
    {
        //given
        var masterPassword = "Shared-Master-Password-1!";
        var storageExternalId = await CreateFullEncryptionStorage(masterPassword);

        var invitedUser = await InviteAndRegisterUser(user: AppOwner);

        //when — invited user unlocks with the same master password
        var invitedUserCookie = await Api.Storages.UnlockFullEncryption(
            externalId: storageExternalId,
            request: new UnlockFullEncryptionRequestDto(MasterPassword: masterPassword),
            cookie: invitedUser.Cookie,
            antiforgery: invitedUser.Antiforgery);

        //then
        invitedUserCookie.Name.Should().Be(FullEncryptionSessionCookie.GetCookieName(storageExternalId));
        invitedUserCookie.Value.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task when_invited_user_unlocks_with_wrong_password_it_fails()
    {
        //given
        var storageExternalId = await CreateFullEncryptionStorage("Correct-Master-Password-1!");

        var invitedUser = await InviteAndRegisterUser(user: AppOwner);

        //when
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.Storages.UnlockFullEncryption(
                externalId: storageExternalId,
                request: new UnlockFullEncryptionRequestDto(MasterPassword: "Wrong-Password-1!"),
                cookie: invitedUser.Cookie,
                antiforgery: invitedUser.Antiforgery));

        //then
        apiError.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        apiError.HttpError.Should().NotBeNull();
        apiError.HttpError!.Code.Should().Be("invalid-master-password");
    }

    [Fact]
    public async Task when_storage_is_unlocked_twice_each_call_returns_a_valid_session_cookie()
    {
        //given
        var masterPassword = "Master-Password-1!";
        var storageExternalId = await CreateFullEncryptionStorage(masterPassword);

        //when — unlock twice
        var firstSession = await Api.Storages.UnlockFullEncryption(
            externalId: storageExternalId,
            request: new UnlockFullEncryptionRequestDto(MasterPassword: masterPassword),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        var secondSession = await Api.Storages.UnlockFullEncryption(
            externalId: storageExternalId,
            request: new UnlockFullEncryptionRequestDto(MasterPassword: masterPassword),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then — both cookies exist and target the same storage
        firstSession.Value.Should().NotBeNullOrWhiteSpace();
        secondSession.Value.Should().NotBeNullOrWhiteSpace();
        firstSession.Name.Should().Be(secondSession.Name);
    }
}
