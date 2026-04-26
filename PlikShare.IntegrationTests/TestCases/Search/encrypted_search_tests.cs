using FluentAssertions;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.Apis;
using PlikShare.Search.Get.Contracts;
using PlikShare.Storages.Encryption;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Search;

[Collection(IntegrationTestsCollection.Name)]
public class encrypted_search_tests : TestFixture
{
    public encrypted_search_tests(
        HostFixture8081 hostFixture,
        ITestOutputHelper testOutputHelper) : base(hostFixture, testOutputHelper)
    {
        hostFixture.ResetUserEncryption().AsTask().Wait();
    }

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

        return (firstOwner, secondOwner, storage);
    }

    [Fact]
    public async Task owner_finds_file_by_decrypted_name_in_full_encrypted_workspace()
    {
        //given
        var (firstOwner, _, storage) = await SetupTwoAppOwnersAndFullEncryptedStorage();

        var workspace = await CreateWorkspace(
            storage: storage,
            user: firstOwner);

        var folder = await CreateFolder(
            workspace: workspace,
            user: firstOwner);

        var fileNameWithoutExt = Random.Name("alpha-secret");
        var fileName = $"{fileNameWithoutExt}.bin";
        var uploadedFile = await UploadFile(
            content: Random.Bytes(64),
            fileName: fileName,
            contentType: "application/octet-stream",
            folder: folder,
            workspace: workspace,
            user: firstOwner);

        //when
        var response = await Api.Search.Search(
            request: new SearchRequestDto
            {
                WorkspaceExternalIds = [],
                BoxExternalIds = [],
                Phrase = "alpha-secret"
            },
            cookie: firstOwner.Cookie,
            antiforgery: firstOwner.Antiforgery,
            userEncryptionSession: firstOwner.EncryptionCookie);

        //then
        var hit = (response.WorkspaceFiles ?? [])
            .SingleOrDefault(f => f.ExternalId == uploadedFile.ExternalId.Value);

        hit.Should().NotBeNull();
        hit!.Name.Should().Be(fileNameWithoutExt);
        hit.Extension.Should().Be(".bin");
        hit.WorkspaceExternalId.Should().Be(workspace.ExternalId.Value);
    }

    [Fact]
    public async Task owner_finds_folder_by_decrypted_name_in_full_encrypted_workspace()
    {
        //given
        var (firstOwner, _, storage) = await SetupTwoAppOwnersAndFullEncryptedStorage();

        var workspace = await CreateWorkspace(
            storage: storage,
            user: firstOwner);

        var folderName = Random.Name("beta-folder");
        var folder = await CreateFolder(
            name: folderName,
            workspace: workspace,
            user: firstOwner);

        //when
        var response = await Api.Search.Search(
            request: new SearchRequestDto
            {
                WorkspaceExternalIds = [],
                BoxExternalIds = [],
                Phrase = "beta-folder"
            },
            cookie: firstOwner.Cookie,
            antiforgery: firstOwner.Antiforgery,
            userEncryptionSession: firstOwner.EncryptionCookie);

        //then
        var hit = (response.WorkspaceFolders ?? [])
            .SingleOrDefault(f => f.ExternalId == folder.ExternalId.Value);

        hit.Should().NotBeNull();
        hit!.Name.Should().Be(folderName);
        hit.WorkspaceExternalId.Should().Be(workspace.ExternalId.Value);
    }

    [Fact]
    public async Task search_returns_decrypted_ancestor_names_for_files_in_nested_folders()
    {
        //given
        var (firstOwner, _, storage) = await SetupTwoAppOwnersAndFullEncryptedStorage();

        var workspace = await CreateWorkspace(
            storage: storage,
            user: firstOwner);

        var parentName = Random.Name("ancestor-parent");
        var parent = await CreateFolder(
            name: parentName,
            workspace: workspace,
            user: firstOwner);

        var child = await CreateFolder(
            parent: parent,
            workspace: workspace,
            user: firstOwner);

        var fileName = $"{Random.Name("ancestor-file")}.bin";
        var uploadedFile = await UploadFile(
            content: Random.Bytes(32),
            fileName: fileName,
            contentType: "application/octet-stream",
            folder: child,
            workspace: workspace,
            user: firstOwner);

        //when
        var response = await Api.Search.Search(
            request: new SearchRequestDto
            {
                WorkspaceExternalIds = [],
                BoxExternalIds = [],
                Phrase = "ancestor-file"
            },
            cookie: firstOwner.Cookie,
            antiforgery: firstOwner.Antiforgery,
            userEncryptionSession: firstOwner.EncryptionCookie);

        //then
        var hit = (response.WorkspaceFiles ?? [])
            .SingleOrDefault(f => f.ExternalId == uploadedFile.ExternalId.Value);

        hit.Should().NotBeNull();
        hit!.FolderPath.Should().HaveCount(2);
        hit.FolderPath[0].Name.Should().Be(parent.Name);
        hit.FolderPath[1].Name.Should().Be(child.Name);
    }

    [Fact]
    public async Task storage_owner_with_sek_only_finds_file_by_decrypted_name()
    {
        //given
        var (firstOwner, secondOwner, storage) = await SetupTwoAppOwnersAndFullEncryptedStorage();

        var workspace = await CreateWorkspace(
            storage: storage,
            user: firstOwner);

        var folder = await CreateFolder(
            workspace: workspace,
            user: firstOwner);

        var fileNameWithoutExt = Random.Name("gamma-shared");
        var fileName = $"{fileNameWithoutExt}.bin";
        var uploadedFile = await UploadFile(
            content: Random.Bytes(64),
            fileName: fileName,
            contentType: "application/octet-stream",
            folder: folder,
            workspace: workspace,
            user: firstOwner);

        HasWorkspaceEncryptionKey(workspace.ExternalId, secondOwner.Email).Should().BeFalse(
            "second owner has no per-workspace wrap; sek-derivation is the only key path");

        //when
        var response = await Api.Search.Search(
            request: new SearchRequestDto
            {
                WorkspaceExternalIds = [],
                BoxExternalIds = [],
                Phrase = "gamma-shared"
            },
            cookie: secondOwner.Cookie,
            antiforgery: secondOwner.Antiforgery,
            userEncryptionSession: secondOwner.EncryptionCookie);

        //then
        var hit = (response.WorkspaceFiles ?? [])
            .SingleOrDefault(f => f.ExternalId == uploadedFile.ExternalId.Value);

        hit.Should().NotBeNull();
        hit!.Name.Should().Be(fileNameWithoutExt);
        hit.Extension.Should().Be(".bin");
    }

    [Fact]
    public async Task admin_without_keys_does_not_see_encrypted_content_in_search()
    {
        //given
        var firstOwnerSignedIn = await SignIn(user: Users.AppOwner);
        var firstOwnerSetup = await Api.UserEncryptionPassword.Setup(
            userExternalId: firstOwnerSignedIn.ExternalId,
            encryptionPassword: DefaultTestEncryptionPassword,
            cookie: firstOwnerSignedIn.Cookie,
            antiforgery: firstOwnerSignedIn.Antiforgery);

        var firstOwner = firstOwnerSignedIn with { EncryptionCookie = firstOwnerSetup.EncryptionCookie };

        // storage created BEFORE second owner sets up encryption — no sek for them
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

        var workspace = await CreateWorkspace(
            storage: storage,
            user: firstOwner);

        var folder = await CreateFolder(
            workspace: workspace,
            user: firstOwner);

        var fileName = $"{Random.Name("delta-locked")}.bin";
        var uploadedFile = await UploadFile(
            content: Random.Bytes(32),
            fileName: fileName,
            contentType: "application/octet-stream",
            folder: folder,
            workspace: workspace,
            user: firstOwner);

        //when
        var response = await Api.Search.Search(
            request: new SearchRequestDto
            {
                WorkspaceExternalIds = [],
                BoxExternalIds = [],
                Phrase = "delta-locked"
            },
            cookie: secondOwner.Cookie,
            antiforgery: secondOwner.Antiforgery);

        //then
        (response.WorkspaceFiles ?? [])
            .Should().NotContain(f => f.ExternalId == uploadedFile.ExternalId.Value,
                "second owner has no key path to this workspace — encrypted content must not surface in search");
    }
}
