using FluentAssertions;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.Apis;
using PlikShare.Search.Get.Contracts;
using PlikShare.Storages.Encryption;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Workspaces;

[Collection(IntegrationTestsCollection.Name)]
public class storage_dek_pending_key_grant_tests : TestFixture
{
    public storage_dek_pending_key_grant_tests(
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

        var sekOwners = GetStorageEncryptionKeyOwnerEmails(storage.ExternalId);
        sekOwners.Should().Contain(firstOwner.Email);
        sekOwners.Should().Contain(secondOwner.Email);

        return (firstOwner, secondOwner, storage);
    }

    [Fact]
    public async Task dashboard_marks_full_encrypted_workspace_as_not_pending_for_storage_owner_with_sek_only()
    {
        //given
        var (firstOwner, secondOwner, storage) = await SetupTwoAppOwnersAndFullEncryptedStorage();

        var workspace = await CreateWorkspace(
            storage: storage,
            user: firstOwner);

        HasWorkspaceEncryptionKey(workspace.ExternalId, secondOwner.Email).Should().BeFalse();

        //when
        var dashboard = await Api.Dashboard.Get(cookie: secondOwner.Cookie);

        //then
        var entry = (dashboard.OtherWorkspaces ?? [])
            .SingleOrDefault(w => w.ExternalId == workspace.ExternalId.Value);

        entry.Should().NotBeNull();
        entry!.IsPendingKeyGrant.Should().BeFalse();
        entry.StorageEncryptionType.Should().Be("full");
    }

    [Fact]
    public async Task dashboard_marks_full_encrypted_workspace_as_pending_for_admin_without_sek_or_wek()
    {
        //given
        var firstOwnerSignedIn = await SignIn(user: Users.AppOwner);
        var firstOwnerSetup = await Api.UserEncryptionPassword.Setup(
            userExternalId: firstOwnerSignedIn.ExternalId,
            encryptionPassword: DefaultTestEncryptionPassword,
            cookie: firstOwnerSignedIn.Cookie,
            antiforgery: firstOwnerSignedIn.Antiforgery);

        var firstOwner = firstOwnerSignedIn with { EncryptionCookie = firstOwnerSetup.EncryptionCookie };

        // storage created BEFORE second owner sets up encryption — no sek row for them
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

        GetStorageEncryptionKeyOwnerEmails(storage.ExternalId)
            .Should().NotContain(secondOwner.Email);

        var workspace = await CreateWorkspace(
            storage: storage,
            user: firstOwner);

        //when
        var dashboard = await Api.Dashboard.Get(cookie: secondOwner.Cookie);

        //then
        var entry = (dashboard.OtherWorkspaces ?? [])
            .SingleOrDefault(w => w.ExternalId == workspace.ExternalId.Value);

        entry.Should().NotBeNull();
        entry!.IsPendingKeyGrant.Should().BeTrue();
    }

    [Fact]
    public async Task search_marks_full_encrypted_workspace_as_not_pending_for_storage_owner_with_sek_only()
    {
        //given
        var (firstOwner, secondOwner, storage) = await SetupTwoAppOwnersAndFullEncryptedStorage();

        var workspace = await CreateWorkspace(
            storage: storage,
            user: firstOwner);

        HasWorkspaceEncryptionKey(workspace.ExternalId, secondOwner.Email).Should().BeFalse();

        //when
        var response = await Api.Search.Search(
            request: new SearchRequestDto
            {
                WorkspaceExternalIds = [],
                BoxExternalIds = [],
                Phrase = workspace.Name
            },
            cookie: secondOwner.Cookie,
            antiforgery: secondOwner.Antiforgery);

        //then
        var hit = (response.Workspaces ?? [])
            .SingleOrDefault(w => w.ExternalId == workspace.ExternalId.Value);

        hit.Should().NotBeNull();
        hit!.IsPendingKeyGrant.Should().BeFalse();
    }
}
