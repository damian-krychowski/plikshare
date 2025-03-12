using FluentAssertions;
using Microsoft.AspNetCore.Http;
using PlikShare.Core.Volumes;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.Apis;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.HardDrive.Create.Contracts;
using PlikShare.Storages.List.Contracts;
using PlikShare.Workspaces.Create.Contracts;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Storages;

[Collection(IntegrationTestsCollection.Name)]
public class storages_tests(
    HostFixture8081 hostFixture,
    ITestOutputHelper testOutputHelper) : TestFixture(hostFixture, testOutputHelper)
{
    [Fact]
    public async Task when_storage_is_created_its_visible_on_the_list()
    {
        //given
        var user = await SignIn(
            user: Users.AppOwner);

        var storageName = Random.Name("hard-drive");

        //when
        var hardDrive = await Api.Storages.CreateHardDriveStorage(
            request: new CreateHardDriveStorageRequestDto(
                Name: storageName,
                VolumePath: MainVolume.Path,
                FolderPath: $"/{storageName}",
                EncryptionType: StorageEncryptionType.None),
            cookie: user.Cookie);

        //then
        var allStorages = await Api.Storages.Get(
            cookie: user.Cookie);

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
        var user = await SignIn(
            user: Users.AppOwner);

        var storageName = Random.Name("hard-drive");

        var hardDrive = await Api.Storages.CreateHardDriveStorage(
            request: new CreateHardDriveStorageRequestDto(
                Name: storageName,
                VolumePath: MainVolume.Path,
                FolderPath: $"/{storageName}",
                EncryptionType: StorageEncryptionType.None),
            cookie: user.Cookie);

        //when
        await Api.Workspaces.Create(
            request: new CreateWorkspaceRequestDto(
                StorageExternalId: hardDrive.ExternalId,
                Name: "my first workspace"),
            cookie: user.Cookie);

        //then
        var storages = await Api.Storages.Get(
            cookie: user.Cookie);

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
        var user = await SignIn(
            user: Users.AppOwner);

        var storageName = Random.Name("hard-drive");

        var hardDrive = await Api.Storages.CreateHardDriveStorage(
            request: new CreateHardDriveStorageRequestDto(
                Name: storageName,
                VolumePath: MainVolume.Path,
                FolderPath: $"/{storageName}",
                EncryptionType: StorageEncryptionType.None),
            cookie: user.Cookie);

        //when
        await Api.Storages.DeleteStorage(
            externalId: hardDrive.ExternalId,
            cookie: user.Cookie);

        //then
        var allStorages = await Api.Storages.Get(
            cookie: user.Cookie);

        allStorages.Items.Should().NotContain(storage => storage.ExternalId == hardDrive.ExternalId);
    }

    [Fact]
    public async Task storage_with_workspace_cannot_be_deleted()
    {
        //given
        var user = await SignIn(
            user: Users.AppOwner);

        var storageName = Random.Name("hard-drive");

        var hardDrive = await Api.Storages.CreateHardDriveStorage(
            request: new CreateHardDriveStorageRequestDto(
                Name: storageName,
                VolumePath: MainVolume.Path,
                FolderPath: $"/{storageName}",
                EncryptionType: StorageEncryptionType.None),
            cookie: user.Cookie);

        await Api.Workspaces.Create(
            request: new CreateWorkspaceRequestDto(
                StorageExternalId: hardDrive.ExternalId,
                Name: "my first workspace"),
            cookie: user.Cookie);

        //when
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.Storages.DeleteStorage(
                externalId: hardDrive.ExternalId,
                cookie: user.Cookie)
            );

        //then
        apiError.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        apiError.HttpError.Should().NotBeNull();
        apiError.HttpError!.Code.Should().Be("storage-has-workspaces-or-integrations-attached");
    }
}