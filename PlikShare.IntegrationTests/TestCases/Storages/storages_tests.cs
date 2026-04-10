using FluentAssertions;
using Microsoft.AspNetCore.Http;
using PlikShare.AuditLog;
using PlikShare.AuditLog.Details;
using PlikShare.Core.Volumes;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.Apis;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.Entities;
using PlikShare.Storages.HardDrive.Create.Contracts;
using PlikShare.Storages.List.Contracts;
using PlikShare.Storages.UpdateName.Contracts;
using PlikShare.Workspaces.Create.Contracts;
using Xunit.Abstractions;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.IntegrationTests.TestCases.Storages;

[Collection(IntegrationTestsCollection.Name)]
public class storages_tests : TestFixture
{
    private AppSignedInUser AppOwner { get; }

    public storages_tests(
        HostFixture8081 hostFixture,
        ITestOutputHelper testOutputHelper) : base(hostFixture, testOutputHelper)
    {
        AppOwner = SignIn(user: Users.AppOwner).Result;
    }

    [Fact]
    public async Task when_storage_is_created_its_visible_on_the_list()
    {
        //given
        var storageName = Random.Name("hard-drive");

        //when
        var hardDrive = await Api.Storages.CreateHardDriveStorage(
            request: new CreateHardDriveStorageRequestDto(
                Name: storageName,
                VolumePath: MainVolume.Path,
                FolderPath: $"/{storageName}",
                EncryptionType: StorageEncryptionType.None),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var allStorages = await Api.Storages.Get(
            cookie: AppOwner.Cookie);

        allStorages.Items.Should().ContainEquivalentOf(new GetHardDriveStorageItemResponseDto
        {
            Name = storageName,
            ExternalId = hardDrive.ExternalId,
            WorkspacesCount = 0,
            EncryptionType = StorageEncryptionType.None,
            FolderPath = Location.NormalizePath($"/{storageName}"),
            VolumePath = Location.NormalizePath(MainVolume.Path),
            FullPath = ""
        }, opt => opt.Excluding(x => x.FullPath));
    }

    [Fact]
    public async Task when_workspace_is_created_storage_workspace_count_should_be_increased()
    {
        //given
        var storageName = Random.Name("hard-drive");

        var hardDrive = await Api.Storages.CreateHardDriveStorage(
            request: new CreateHardDriveStorageRequestDto(
                Name: storageName,
                VolumePath: MainVolume.Path,
                FolderPath: $"/{storageName}",
                EncryptionType: StorageEncryptionType.None),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //when
        await Api.Workspaces.Create(
            request: new CreateWorkspaceRequestDto(
                StorageExternalId: hardDrive.ExternalId,
                Name: "my first workspace"),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var storages = await Api.Storages.Get(
            cookie: AppOwner.Cookie);

        storages.Items.Should().ContainEquivalentOf(new GetHardDriveStorageItemResponseDto
        {
            Name = storageName,
            ExternalId = hardDrive.ExternalId,
            WorkspacesCount = 1,
            EncryptionType = StorageEncryptionType.None,
            FolderPath = Location.NormalizePath($"/{storageName}"),
            VolumePath = Location.NormalizePath(MainVolume.Path),
            FullPath = ""
        }, opt => opt.Excluding(x => x.FullPath));
    }

    [Fact]
    public async Task when_storage_is_deleted_its_no_longer_visible_on_the_list()
    {
        //given
        var storageName = Random.Name("hard-drive");

        var hardDrive = await Api.Storages.CreateHardDriveStorage(
            request: new CreateHardDriveStorageRequestDto(
                Name: storageName,
                VolumePath: MainVolume.Path,
                FolderPath: $"/{storageName}",
                EncryptionType: StorageEncryptionType.None),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //when
        await Api.Storages.DeleteStorage(
            externalId: hardDrive.ExternalId,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var allStorages = await Api.Storages.Get(
            cookie: AppOwner.Cookie);

        allStorages.Items.Should().NotContain(storage => storage.ExternalId == hardDrive.ExternalId);
    }

    [Fact]
    public async Task storage_with_workspace_cannot_be_deleted()
    {
        //given
        var storageName = Random.Name("hard-drive");

        var hardDrive = await Api.Storages.CreateHardDriveStorage(
            request: new CreateHardDriveStorageRequestDto(
                Name: storageName,
                VolumePath: MainVolume.Path,
                FolderPath: $"/{storageName}",
                EncryptionType: StorageEncryptionType.None),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        await Api.Workspaces.Create(
            request: new CreateWorkspaceRequestDto(
                StorageExternalId: hardDrive.ExternalId,
                Name: "my first workspace"),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //when
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.Storages.DeleteStorage(
                externalId: hardDrive.ExternalId,
                cookie: AppOwner.Cookie,
                antiforgery: AppOwner.Antiforgery)
            );

        //then
        apiError.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        apiError.HttpError.Should().NotBeNull();
        apiError.HttpError!.Code.Should().Be("storage-has-workspaces-or-integrations-attached");
    }

    [Fact]
    public async Task when_storage_name_is_updated_it_is_reflected_on_the_list()
    {
        //given
        var storageName = Random.Name("hard-drive");

        var hardDrive = await Api.Storages.CreateHardDriveStorage(
            request: new CreateHardDriveStorageRequestDto(
                Name: storageName,
                VolumePath: MainVolume.Path,
                FolderPath: $"/{storageName}",
                EncryptionType: StorageEncryptionType.None),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        var newName = Random.Name("renamed-storage");

        //when
        await Api.Storages.UpdateName(
            externalId: hardDrive.ExternalId,
            request: new UpdateStorageNameRequestDto(
                Name: newName),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var allStorages = await Api.Storages.Get(
            cookie: AppOwner.Cookie);

        allStorages.Items.Should().Contain(s => s.ExternalId == hardDrive.ExternalId && s.Name == newName);
    }

    [Fact]
    public async Task creating_storage_should_produce_audit_log_entry()
    {
        //given
        var storageName = Random.Name("hard-drive");

        //when
        await Api.Storages.CreateHardDriveStorage(
            request: new CreateHardDriveStorageRequestDto(
                Name: storageName,
                VolumePath: MainVolume.Path,
                FolderPath: $"/{storageName}",
                EncryptionType: StorageEncryptionType.None),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<Audit.Storage.Created>(
            expectedEventType: AuditLogEventTypes.Storage.Created,
            assertDetails: details =>
            {
                details.Storage.Name.Should().Be(storageName);
                details.Storage.Type.Should().Be(StorageType.HardDrive);
            },
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Info);
    }

    [Fact]
    public async Task deleting_storage_should_produce_audit_log_entry()
    {
        //given
        var storageName = Random.Name("hard-drive");

        var hardDrive = await Api.Storages.CreateHardDriveStorage(
            request: new CreateHardDriveStorageRequestDto(
                Name: storageName,
                VolumePath: MainVolume.Path,
                FolderPath: $"/{storageName}",
                EncryptionType: StorageEncryptionType.None),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //when
        await Api.Storages.DeleteStorage(
            externalId: hardDrive.ExternalId,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<Audit.Storage.Deleted>(
            expectedEventType: AuditLogEventTypes.Storage.Deleted,
            assertDetails: details => details.Storage.ExternalId.Should().Be(hardDrive.ExternalId),
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Warning);
    }

    [Fact]
    public async Task updating_storage_name_should_produce_audit_log_entry()
    {
        //given
        var storageName = Random.Name("hard-drive");

        var hardDrive = await Api.Storages.CreateHardDriveStorage(
            request: new CreateHardDriveStorageRequestDto(
                Name: storageName,
                VolumePath: MainVolume.Path,
                FolderPath: $"/{storageName}",
                EncryptionType: StorageEncryptionType.None),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        var newName = Random.Name("renamed-storage");

        //when
        await Api.Storages.UpdateName(
            externalId: hardDrive.ExternalId,
            request: new UpdateStorageNameRequestDto(
                Name: newName),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<Audit.Storage.NameUpdated>(
            expectedEventType: AuditLogEventTypes.Storage.NameUpdated,
            assertDetails: details =>
            {
                details.Storage.ExternalId.Should().Be(hardDrive.ExternalId);
                details.Storage.Name.Should().Be(newName);
            },
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Info);
    }
}
