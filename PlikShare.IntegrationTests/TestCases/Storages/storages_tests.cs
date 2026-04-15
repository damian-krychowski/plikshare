using FluentAssertions;
using Microsoft.AspNetCore.Http;
using PlikShare.Core.Volumes;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.Apis;
using PlikShare.Storages.AzureBlob.Create.Contracts;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.HardDrive.Create.Contracts;
using PlikShare.Storages.List.Contracts;
using PlikShare.Workspaces.Create.Contracts;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Storages;

[Collection(IntegrationTestsCollection.Name)]
public class storages_tests : TestFixture
{
    private readonly ITestOutputHelper _testOutputHelper;

    public storages_tests(HostFixture8081 hostFixture, ITestOutputHelper testOutputHelper)
        : base(hostFixture, testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }
    private const string AzureBlobAccountNameEnv = "PLIKSHARE_TESTS_AZURE_BLOB_ACCOUNT_NAME";
    private const string AzureBlobAccountKeyEnv = "PLIKSHARE_TESTS_AZURE_BLOB_ACCOUNT_KEY";
    private const string AzureBlobServiceUrlEnv = "PLIKSHARE_TESTS_AZURE_BLOB_SERVICE_URL";

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
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

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
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        //when
        await Api.Workspaces.Create(
            request: new CreateWorkspaceRequestDto(
                StorageExternalId: hardDrive.ExternalId,
                Name: "my first workspace"),
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

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
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        //when
        await Api.Storages.DeleteStorage(
            externalId: hardDrive.ExternalId,
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

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
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        await Api.Workspaces.Create(
            request: new CreateWorkspaceRequestDto(
                StorageExternalId: hardDrive.ExternalId,
                Name: "my first workspace"),
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        //when
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.Storages.DeleteStorage(
                externalId: hardDrive.ExternalId,
                cookie: user.Cookie,
                antiforgery: user.Antiforgery)
            );

        //then
        apiError.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        apiError.HttpError.Should().NotBeNull();
        apiError.HttpError!.Code.Should().Be("storage-has-workspaces-or-integrations-attached");
    }

    [Fact]
    public async Task when_azure_blob_storage_is_created_with_invalid_url_then_request_fails()
    {
        //given
        var user = await SignIn(
            user: Users.AppOwner);

        //when
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.Storages.CreateAzureBlobStorage(
                request: new CreateAzureBlobStorageRequestDto(
                    Name: Random.Name("azure-blob"),
                    AccountName: "dummy-account",
                    AccountKey: "dGVzdA==",
                    ServiceUrl: "ht!tp://invalid",
                    EncryptionType: StorageEncryptionType.None),
                cookie: user.Cookie,
                antiforgery: user.Antiforgery)
            );

        //then
        apiError.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        apiError.HttpError.Should().NotBeNull();
        apiError.HttpError!.Code.Should().BeOneOf(
            "storage-url-invalid",
            "storage-invalid-url",
            "storage-connection-failed");
    }

    [Fact]
    public async Task when_azure_blob_storage_is_created_with_valid_credentials_its_visible_on_the_list()
    {
        //given
        if (!TryGetAzureBlobTestConfig(out var azure))
        {
            _testOutputHelper.WriteLine(
                $"Skipping test. Missing env vars: {AzureBlobAccountNameEnv}, {AzureBlobAccountKeyEnv}, {AzureBlobServiceUrlEnv}");
            return;
        }

        var user = await SignIn(
            user: Users.AppOwner);

        var storageName = Random.Name("azure-blob");

        //when
        var created = await Api.Storages.CreateAzureBlobStorage(
            request: new CreateAzureBlobStorageRequestDto(
                Name: storageName,
                AccountName: azure.AccountName,
                AccountKey: azure.AccountKey,
                ServiceUrl: azure.ServiceUrl,
                EncryptionType: StorageEncryptionType.None),
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        //then
        var allStorages = await Api.Storages.Get(
            cookie: user.Cookie);

        allStorages.Items.Should().ContainEquivalentOf(new GetAzureBlobStorageItemResponseDto
        {
            Name = storageName,
            ExternalId = created.ExternalId,
            WorkspacesCount = 0,
            EncryptionType = StorageEncryptionType.None,
            AuthType = "shared-key",
            AccountName = azure.AccountName,
            ServiceUrl = azure.ServiceUrl
        });
    }

    private static bool TryGetAzureBlobTestConfig(out AzureBlobTestConfig config)
    {
        var accountName = Environment.GetEnvironmentVariable(AzureBlobAccountNameEnv);
        var accountKey = Environment.GetEnvironmentVariable(AzureBlobAccountKeyEnv);
        var serviceUrl = Environment.GetEnvironmentVariable(AzureBlobServiceUrlEnv);

        if (string.IsNullOrWhiteSpace(accountName)
            || string.IsNullOrWhiteSpace(accountKey)
            || string.IsNullOrWhiteSpace(serviceUrl))
        {
            config = default;
            return false;
        }

        config = new AzureBlobTestConfig(
            AccountName: accountName,
            AccountKey: accountKey,
            ServiceUrl: serviceUrl);

        return true;
    }

    private readonly record struct AzureBlobTestConfig(
        string AccountName,
        string AccountKey,
        string ServiceUrl);
}