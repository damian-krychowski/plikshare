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
        HostFixture.ResetUserEncryption().AsTask().Wait();
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
    public async Task when_storage_is_created_without_encryption_recovery_code_is_not_returned()
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
        hardDrive.RecoveryCode.Should().BeNull();
    }

    [Fact]
    public async Task when_storage_is_created_with_managed_encryption_recovery_code_is_returned()
    {
        //given
        var storageName = Random.Name("hard-drive");

        //when
        var hardDrive = await Api.Storages.CreateHardDriveStorage(
            request: new CreateHardDriveStorageRequestDto(
                Name: storageName,
                VolumePath: MainVolume.Path,
                FolderPath: $"/{storageName}",
                EncryptionType: StorageEncryptionType.Managed),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        hardDrive.RecoveryCode.Should().NotBeNullOrWhiteSpace();

        var words = hardDrive.RecoveryCode!.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        words.Should().HaveCount(24);

        var allStorages = await Api.Storages.Get(
            cookie: AppOwner.Cookie);

        allStorages.Items.Should().Contain(s =>
            s.ExternalId == hardDrive.ExternalId &&
            s.EncryptionType == StorageEncryptionType.Managed);
    }

    [Fact]
    public async Task when_full_encryption_storage_is_created_without_encryption_password_setup_it_fails()
    {
        //given
        var storageName = Random.Name("hard-drive");

        //when
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.Storages.CreateHardDriveStorage(
                request: new CreateHardDriveStorageRequestDto(
                    Name: storageName,
                    VolumePath: MainVolume.Path,
                    FolderPath: $"/{storageName}",
                    EncryptionType: StorageEncryptionType.Full),
                cookie: AppOwner.Cookie,
                antiforgery: AppOwner.Antiforgery));

        //then
        apiError.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        apiError.HttpError.Should().NotBeNull();
        apiError.HttpError!.Code.Should().Be("creator-encryption-not-set-up");
    }

    [Fact]
    public async Task when_full_encryption_storage_is_created_after_setup_recovery_code_is_returned()
    {
        //given
        var (owner, _) = await SetupUserEncryptionPassword(AppOwner);
        var storageName = Random.Name("hard-drive");

        //when
        var hardDrive = await Api.Storages.CreateHardDriveStorage(
            request: new CreateHardDriveStorageRequestDto(
                Name: storageName,
                VolumePath: MainVolume.Path,
                FolderPath: $"/{storageName}",
                EncryptionType: StorageEncryptionType.Full),
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        //then
        hardDrive.RecoveryCode.Should().NotBeNullOrWhiteSpace();

        var words = hardDrive.RecoveryCode!.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        words.Should().HaveCount(24);

        var allStorages = await Api.Storages.Get(
            cookie: owner.Cookie);

        allStorages.Items.Should().Contain(s =>
            s.ExternalId == hardDrive.ExternalId &&
            s.EncryptionType == StorageEncryptionType.Full);
    }

    [Fact]
    public async Task when_two_full_encryption_storages_are_created_their_recovery_codes_differ()
    {
        //given
        var (owner, _) = await SetupUserEncryptionPassword(AppOwner);

        //when
        var first = await Api.Storages.CreateHardDriveStorage(
            request: new CreateHardDriveStorageRequestDto(
                Name: Random.Name("hard-drive"),
                VolumePath: MainVolume.Path,
                FolderPath: $"/{Random.Name("hard-drive")}",
                EncryptionType: StorageEncryptionType.Full),
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        var second = await Api.Storages.CreateHardDriveStorage(
            request: new CreateHardDriveStorageRequestDto(
                Name: Random.Name("hard-drive"),
                VolumePath: MainVolume.Path,
                FolderPath: $"/{Random.Name("hard-drive")}",
                EncryptionType: StorageEncryptionType.Full),
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        //then
        first.RecoveryCode.Should().NotBeNullOrWhiteSpace();
        second.RecoveryCode.Should().NotBeNullOrWhiteSpace();
        first.RecoveryCode.Should().NotBe(second.RecoveryCode);
    }

    [Fact]
    public async Task when_workspace_is_created_on_full_encryption_storage_without_encryption_cookie_it_fails()
    {
        //given
        var (owner, _) = await SetupUserEncryptionPassword(AppOwner);

        var storage = await CreateHardDriveStorage(
            user: owner,
            encryptionType: StorageEncryptionType.Full);

        //when
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.Workspaces.Create(
                request: new CreateWorkspaceRequestDto(
                    StorageExternalId: storage.ExternalId,
                    Name: "full workspace no cookie"),
                cookie: owner.Cookie,
                antiforgery: owner.Antiforgery,
                userEncryptionSession: null));

        //then
        apiError.StatusCode.Should().Be(StatusCodes.Status423Locked);
        apiError.HttpError.Should().NotBeNull();
        apiError.HttpError!.Code.Should().Be("user-encryption-session-required");
    }

    [Fact]
    public async Task workspace_on_full_encryption_storage_cannot_be_accessed_without_user_encryption_cookie()
    {
        //given
        var (owner, _) = await SetupUserEncryptionPassword(AppOwner);

        var storage = await CreateHardDriveStorage(
            user: owner,
            encryptionType: StorageEncryptionType.Full);

        var workspace = await CreateWorkspace(
            storage: storage,
            user: owner);

        var folder = await CreateFolder(
            workspace: workspace,
            user: owner);

        var content = Random.Bytes(512);
        var uploadedFile = await UploadFile(
            content: content,
            fileName: $"{Random.Name("full")}.bin",
            contentType: "application/octet-stream",
            folder: folder,
            workspace: workspace,
            user: owner);

        //when
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.Files.GetDownloadLink(
                workspaceExternalId: workspace.ExternalId,
                fileExternalId: uploadedFile.ExternalId,
                contentDisposition: "attachment",
                cookie: owner.Cookie,
                workspaceEncryptionSession: null));

        //then
        apiError.StatusCode.Should().Be(StatusCodes.Status423Locked);
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

    [Fact]
    public async Task full_encryption_storage_should_have_encryption_keys_for_all_app_owners_with_encryption_configured()
    {
        //given
        var ownerSetupResult = await Api.UserEncryptionPassword.Setup(
            userExternalId: AppOwner.ExternalId,
            encryptionPassword: DefaultTestEncryptionPassword,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        var owner = AppOwner with { EncryptionCookie = ownerSetupResult.EncryptionCookie };

        var secondOwner = await SignIn(user: Users.SecondAppOwner);

        await Api.UserEncryptionPassword.Setup(
            userExternalId: secondOwner.ExternalId,
            encryptionPassword: DefaultTestEncryptionPassword,
            cookie: secondOwner.Cookie,
            antiforgery: secondOwner.Antiforgery);

        var storageName = Random.Name("hard-drive");

        //when
        var hardDrive = await Api.Storages.CreateHardDriveStorage(
            request: new CreateHardDriveStorageRequestDto(
                Name: storageName,
                VolumePath: MainVolume.Path,
                FolderPath: $"/{storageName}",
                EncryptionType: StorageEncryptionType.Full),
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        //then
        var keyOwnerEmails = GetStorageEncryptionKeyOwnerEmails(hardDrive.ExternalId);

        keyOwnerEmails.Should().HaveCount(2);
        keyOwnerEmails.Should().Contain(Users.AppOwner.Email);
        keyOwnerEmails.Should().Contain(Users.SecondAppOwner.Email);
    }

    [Fact]
    public async Task full_encryption_storage_should_skip_app_owners_without_encryption_configured()
    {
        //given
        var (owner, _) = await SetupUserEncryptionPassword(AppOwner);

        // second app owner exists but has NOT set up encryption password
        var storageName = Random.Name("hard-drive");

        //when
        var hardDrive = await Api.Storages.CreateHardDriveStorage(
            request: new CreateHardDriveStorageRequestDto(
                Name: storageName,
                VolumePath: MainVolume.Path,
                FolderPath: $"/{storageName}",
                EncryptionType: StorageEncryptionType.Full),
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        //then
        var keyOwnerEmails = GetStorageEncryptionKeyOwnerEmails(hardDrive.ExternalId);

        keyOwnerEmails.Should().HaveCount(1);
        keyOwnerEmails.Should().Contain(Users.AppOwner.Email);
        keyOwnerEmails.Should().NotContain(Users.SecondAppOwner.Email);
    }
}
