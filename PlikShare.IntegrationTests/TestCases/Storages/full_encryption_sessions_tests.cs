using FluentAssertions;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.Apis;
using PlikShare.Storages;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.HardDrive.Create.Contracts;
using PlikShare.Storages.Id;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Storages;

[Collection(IntegrationTestsCollection.Name)]
public class full_encryption_sessions_tests : TestFixture
{
    private AppSignedInUser AppOwner { get; }

    public full_encryption_sessions_tests(
        HostFixture8081 hostFixture,
        ITestOutputHelper testOutputHelper) : base(hostFixture, testOutputHelper)
    {
        AppOwner = SignIn(user: Users.AppOwner).Result;
    }

    private async Task<(StorageExtId StorageExternalId, GenericCookie SessionCookie)>
        CreateAndUnlockFullEncryptionStorage(string masterPassword)
    {
        var storageName = Random.Name("hard-drive");

        var createResponse = await Api.Storages.CreateHardDriveStorage(
            request: new CreateHardDriveStorageRequestDto(
                Name: storageName,
                VolumePath: MainVolume.Path,
                FolderPath: $"/{storageName}",
                EncryptionType: StorageEncryptionType.Full,
                MasterPassword: masterPassword),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        var sessionCookie = await Api.Storages.UnlockFullEncryption(
            externalId: createResponse.ExternalId,
            request: new UnlockFullEncryptionRequestDto(MasterPassword: masterPassword),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        return (createResponse.ExternalId, sessionCookie);
    }

    [Fact]
    public async Task when_no_storages_are_unlocked_listing_returns_empty_items()
    {
        //given — no unlock calls made

        //when
        var sessions = await Api.FullEncryptionSessions.Get(cookie: AppOwner.Cookie);

        //then
        sessions.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task when_one_storage_is_unlocked_listing_shows_it()
    {
        //given
        var (storageId, sessionCookie) = await CreateAndUnlockFullEncryptionStorage("Password-1!");

        //when
        var sessions = await Api.FullEncryptionSessions.Get(
            cookie: AppOwner.Cookie,
            fullEncryptionSessions: sessionCookie);

        //then
        sessions.Items.Should().HaveCount(1);
        sessions.Items.Should().Contain(i => i.StorageExternalId == storageId);
    }

    [Fact]
    public async Task when_multiple_storages_are_unlocked_listing_shows_all_of_them()
    {
        //given
        var (storageA, sessionA) = await CreateAndUnlockFullEncryptionStorage("Password-A-1!");
        var (storageB, sessionB) = await CreateAndUnlockFullEncryptionStorage("Password-B-1!");
        var (storageC, sessionC) = await CreateAndUnlockFullEncryptionStorage("Password-C-1!");

        //when
        var sessions = await Api.FullEncryptionSessions.Get(
            cookie: AppOwner.Cookie,
            fullEncryptionSessions: [sessionA, sessionB, sessionC]);

        //then
        sessions.Items.Should().HaveCount(3);
        sessions.Items.Select(i => i.StorageExternalId).Should().BeEquivalentTo(
            [storageA, storageB, storageC]);
    }

    [Fact]
    public async Task listing_includes_the_storage_name_for_each_unlocked_entry()
    {
        //given
        var storageName = Random.Name("hard-drive");
        var createResponse = await Api.Storages.CreateHardDriveStorage(
            request: new CreateHardDriveStorageRequestDto(
                Name: storageName,
                VolumePath: MainVolume.Path,
                FolderPath: $"/{storageName}",
                EncryptionType: StorageEncryptionType.Full,
                MasterPassword: "Password-1!"),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        var sessionCookie = await Api.Storages.UnlockFullEncryption(
            externalId: createResponse.ExternalId,
            request: new UnlockFullEncryptionRequestDto(MasterPassword: "Password-1!"),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //when
        var sessions = await Api.FullEncryptionSessions.Get(
            cookie: AppOwner.Cookie,
            fullEncryptionSessions: sessionCookie);

        //then
        sessions.Items.Should().ContainSingle()
            .Which.StorageName.Should().Be(storageName);
    }

    [Fact]
    public async Task when_single_storage_is_locked_it_is_removed_from_listing()
    {
        //given
        var (storageA, sessionA) = await CreateAndUnlockFullEncryptionStorage("Password-A-1!");
        var (storageB, sessionB) = await CreateAndUnlockFullEncryptionStorage("Password-B-1!");

        //when — lock A only
        await Api.FullEncryptionSessions.Lock(
            storageExternalId: storageA,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery,
            fullEncryptionSessions: [sessionA, sessionB]);

        //then — listing still sees B because its cookie is still present
        var sessions = await Api.FullEncryptionSessions.Get(
            cookie: AppOwner.Cookie,
            fullEncryptionSessions: sessionB);

        sessions.Items.Should().HaveCount(1);
        sessions.Items.Should().Contain(i => i.StorageExternalId == storageB);
        sessions.Items.Should().NotContain(i => i.StorageExternalId == storageA);
    }

    [Fact]
    public async Task when_all_storages_are_locked_listing_becomes_empty()
    {
        //given
        var (_, sessionA) = await CreateAndUnlockFullEncryptionStorage("Password-A-1!");
        var (_, sessionB) = await CreateAndUnlockFullEncryptionStorage("Password-B-1!");

        //when
        await Api.FullEncryptionSessions.LockAll(
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery,
            fullEncryptionSessions: [sessionA, sessionB]);

        //then — without any cookie left on the client, listing is empty
        var sessions = await Api.FullEncryptionSessions.Get(cookie: AppOwner.Cookie);

        sessions.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task locking_a_storage_that_was_not_unlocked_is_idempotent()
    {
        //given
        var storageName = Random.Name("hard-drive");
        var createResponse = await Api.Storages.CreateHardDriveStorage(
            request: new CreateHardDriveStorageRequestDto(
                Name: storageName,
                VolumePath: MainVolume.Path,
                FolderPath: $"/{storageName}",
                EncryptionType: StorageEncryptionType.Full,
                MasterPassword: "Password-1!"),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //when — lock without ever unlocking
        await Api.FullEncryptionSessions.Lock(
            storageExternalId: createResponse.ExternalId,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then — call succeeds, listing still empty
        var sessions = await Api.FullEncryptionSessions.Get(cookie: AppOwner.Cookie);
        sessions.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task lock_all_with_no_sessions_is_idempotent()
    {
        //given — no unlock calls made

        //when
        await Api.FullEncryptionSessions.LockAll(
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var sessions = await Api.FullEncryptionSessions.Get(cookie: AppOwner.Cookie);
        sessions.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task listing_filters_out_sessions_for_deleted_storages()
    {
        //given — unlock a storage, then delete it
        var (storageId, sessionCookie) = await CreateAndUnlockFullEncryptionStorage("Password-1!");

        await Api.Storages.DeleteStorage(
            externalId: storageId,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //when — cookie is still on the client, but storage no longer exists
        var sessions = await Api.FullEncryptionSessions.Get(
            cookie: AppOwner.Cookie,
            fullEncryptionSessions: sessionCookie);

        //then — endpoint drops entries for storages no longer in the store
        sessions.Items.Should().NotContain(i => i.StorageExternalId == storageId);
    }

    [Fact]
    public async Task each_user_sees_only_their_own_unlocked_sessions()
    {
        //given — app owner unlocks storage; invited user unlocks a different storage
        var (storageA, sessionA_owner) = await CreateAndUnlockFullEncryptionStorage("Password-A-1!");
        var (storageB, _) = await CreateAndUnlockFullEncryptionStorage("Password-B-1!");

        var invitedUser = await InviteAndRegisterUser(user: AppOwner);

        var sessionB_invitedUser = await Api.Storages.UnlockFullEncryption(
            externalId: storageB,
            request: new UnlockFullEncryptionRequestDto(MasterPassword: "Password-B-1!"),
            cookie: invitedUser.Cookie,
            antiforgery: invitedUser.Antiforgery);

        //when — each user calls listing with their own cookies
        var ownerSessions = await Api.FullEncryptionSessions.Get(
            cookie: AppOwner.Cookie,
            fullEncryptionSessions: sessionA_owner);

        var invitedSessions = await Api.FullEncryptionSessions.Get(
            cookie: invitedUser.Cookie,
            fullEncryptionSessions: sessionB_invitedUser);

        //then — sessions are cookie-scoped per-client; each user sees only their own
        ownerSessions.Items.Should().ContainSingle()
            .Which.StorageExternalId.Should().Be(storageA);

        invitedSessions.Items.Should().ContainSingle()
            .Which.StorageExternalId.Should().Be(storageB);
    }
}
