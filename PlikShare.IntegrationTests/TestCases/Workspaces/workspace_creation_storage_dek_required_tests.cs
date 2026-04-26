using FluentAssertions;
using Microsoft.AspNetCore.Http;
using PlikShare.Core.Utils;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.Apis;
using PlikShare.Storages.Encryption;
using PlikShare.Users.PermissionsAndRoles;
using PlikShare.Users.UpdateMaxWorkspaceNumber.Contracts;
using PlikShare.Workspaces.Create.Contracts;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Workspaces;

/// <summary>
/// On a full-encryption storage, holding the <c>CanAddWorkspace</c> permission is not
/// enough on its own — the caller must also hold a sek wrap, otherwise
/// <c>WorkspaceCreationPreparation</c> has no Storage DEK to derive the new workspace's
/// DEK from and short-circuits with 403 not-a-storage-admin.
/// </summary>
[Collection(IntegrationTestsCollection.Name)]
public class workspace_creation_storage_dek_required_tests : TestFixture
{
    public workspace_creation_storage_dek_required_tests(
        HostFixture8081 hostFixture,
        ITestOutputHelper testOutputHelper) : base(hostFixture, testOutputHelper)
    {
        hostFixture.ResetUserEncryption().AsTask().Wait();
    }

    [Fact]
    public async Task user_with_can_add_workspace_cannot_create_workspace_in_full_encrypted_storage_without_sek()
    {
        //given
        var ownerSignedIn = await SignIn(user: Users.AppOwner);
        var ownerSetup = await Api.UserEncryptionPassword.Setup(
            userExternalId: ownerSignedIn.ExternalId,
            encryptionPassword: DefaultTestEncryptionPassword,
            cookie: ownerSignedIn.Cookie,
            antiforgery: ownerSignedIn.Antiforgery);

        var owner = ownerSignedIn with { EncryptionCookie = ownerSetup.EncryptionCookie };

        var storage = await CreateHardDriveStorage(
            user: owner,
            encryptionType: StorageEncryptionType.Full);

        var regularUser = await InviteAndRegisterUser(owner);

        await Api.Users.UpdatePermissionsAndRoles(
            userExternalId: regularUser.ExternalId,
            request: new UserPermissionsAndRolesDto
            {
                IsAdmin = false,
                CanAddWorkspace = true,
                CanManageAuth = false,
                CanManageIntegrations = false,
                CanManageEmailProviders = false,
                CanManageGeneralSettings = false,
                CanManageStorages = false,
                CanManageUsers = false,
                CanManageAuditLog = false
            },
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        await Api.Users.UpdateMaxWorkspaceNumber(
            userExternalId: regularUser.ExternalId,
            request: new UpdateUserMaxWorkspaceNumberRequestDto { MaxWorkspaceNumber = 1 },
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        var regularUserSetup = await Api.UserEncryptionPassword.Setup(
            userExternalId: regularUser.ExternalId,
            encryptionPassword: DefaultTestEncryptionPassword,
            cookie: regularUser.Cookie,
            antiforgery: regularUser.Antiforgery);

        var regularUserWithEncryption = regularUser with { EncryptionCookie = regularUserSetup.EncryptionCookie };

        GetStorageEncryptionKeyOwnerEmails(storage.ExternalId)
            .Should().NotContain(regularUserWithEncryption.Email,
                "regular non-admin users do not get a sek wrap when their encryption is set up after the storage exists");

        //when
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.Workspaces.Create(
                request: new CreateWorkspaceRequestDto(
                    StorageExternalId: storage.ExternalId,
                    Name: $"workspace-{Guid.NewGuid().ToBase62()}"),
                cookie: regularUserWithEncryption.Cookie,
                antiforgery: regularUserWithEncryption.Antiforgery,
                userEncryptionSession: regularUserWithEncryption.EncryptionCookie));

        //then
        apiError.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        apiError.HttpError.Should().NotBeNull();
        apiError.HttpError!.Code.Should().Be("not-a-storage-admin");
    }

    [Fact]
    public async Task user_with_can_add_workspace_can_still_create_workspace_in_unencrypted_storage()
    {
        //given — the sek-required rule is scoped to full-encryption storages; None/Managed
        // storages have no DEK concept and must remain creatable by any holder of the
        // permission.
        var owner = await SignIn(user: Users.AppOwner);

        var storage = await CreateHardDriveStorage(
            user: owner);

        var regularUser = await InviteAndRegisterUser(owner);

        await Api.Users.UpdatePermissionsAndRoles(
            userExternalId: regularUser.ExternalId,
            request: new UserPermissionsAndRolesDto
            {
                IsAdmin = false,
                CanAddWorkspace = true,
                CanManageAuth = false,
                CanManageIntegrations = false,
                CanManageEmailProviders = false,
                CanManageGeneralSettings = false,
                CanManageStorages = false,
                CanManageUsers = false,
                CanManageAuditLog = false
            },
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        await Api.Users.UpdateMaxWorkspaceNumber(
            userExternalId: regularUser.ExternalId,
            request: new UpdateUserMaxWorkspaceNumberRequestDto { MaxWorkspaceNumber = 1 },
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        //when
        var workspace = await Api.Workspaces.Create(
            request: new CreateWorkspaceRequestDto(
                StorageExternalId: storage.ExternalId,
                Name: $"workspace-{Guid.NewGuid().ToBase62()}"),
            cookie: regularUser.Cookie,
            antiforgery: regularUser.Antiforgery);

        //then
        workspace.ExternalId.Value.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task dashboard_hides_full_encrypted_storages_from_users_without_sek()
    {
        //given — storage is created BEFORE the second owner sets up encryption, so no
        // sek wrap exists for them. The dashboard storage list (used by the UI workspace-
        // creation picker) must hide storages the caller could not actually create a
        // workspace in.
        var firstOwnerSignedIn = await SignIn(user: Users.AppOwner);
        var firstOwnerSetup = await Api.UserEncryptionPassword.Setup(
            userExternalId: firstOwnerSignedIn.ExternalId,
            encryptionPassword: DefaultTestEncryptionPassword,
            cookie: firstOwnerSignedIn.Cookie,
            antiforgery: firstOwnerSignedIn.Antiforgery);

        var firstOwner = firstOwnerSignedIn with { EncryptionCookie = firstOwnerSetup.EncryptionCookie };

        var fullStorage = await CreateHardDriveStorage(
            user: firstOwner,
            encryptionType: StorageEncryptionType.Full);

        var plainStorage = await CreateHardDriveStorage(
            user: firstOwner);

        var secondOwnerSignedIn = await SignIn(user: Users.SecondAppOwner);
        await Api.UserEncryptionPassword.Setup(
            userExternalId: secondOwnerSignedIn.ExternalId,
            encryptionPassword: DefaultTestEncryptionPassword,
            cookie: secondOwnerSignedIn.Cookie,
            antiforgery: secondOwnerSignedIn.Antiforgery);

        GetStorageEncryptionKeyOwnerEmails(fullStorage.ExternalId)
            .Should().NotContain(secondOwnerSignedIn.Email);

        //when
        var dashboard = await Api.Dashboard.Get(cookie: secondOwnerSignedIn.Cookie);

        //then
        var visible = dashboard.Storages ?? [];
        visible.Should().NotContain(s => s.ExternalId == fullStorage.ExternalId.Value,
            "no sek wrap means workspace creation here would fail with not-a-storage-admin");
        visible.Should().Contain(s => s.ExternalId == plainStorage.ExternalId.Value,
            "non-Full storages have no DEK concept and stay selectable");
    }

    [Fact]
    public async Task dashboard_shows_full_encrypted_storage_to_users_with_sek()
    {
        //given — both owners have encryption set up before storage creation, so both
        // pick up a sek wrap and the storage stays selectable for both.
        var firstOwnerSignedIn = await SignIn(user: Users.AppOwner);
        var firstOwnerSetup = await Api.UserEncryptionPassword.Setup(
            userExternalId: firstOwnerSignedIn.ExternalId,
            encryptionPassword: DefaultTestEncryptionPassword,
            cookie: firstOwnerSignedIn.Cookie,
            antiforgery: firstOwnerSignedIn.Antiforgery);

        var firstOwner = firstOwnerSignedIn with { EncryptionCookie = firstOwnerSetup.EncryptionCookie };

        var secondOwnerSignedIn = await SignIn(user: Users.SecondAppOwner);
        await Api.UserEncryptionPassword.Setup(
            userExternalId: secondOwnerSignedIn.ExternalId,
            encryptionPassword: DefaultTestEncryptionPassword,
            cookie: secondOwnerSignedIn.Cookie,
            antiforgery: secondOwnerSignedIn.Antiforgery);

        var fullStorage = await CreateHardDriveStorage(
            user: firstOwner,
            encryptionType: StorageEncryptionType.Full);

        GetStorageEncryptionKeyOwnerEmails(fullStorage.ExternalId)
            .Should().Contain(secondOwnerSignedIn.Email);

        //when
        var dashboard = await Api.Dashboard.Get(cookie: secondOwnerSignedIn.Cookie);

        //then
        (dashboard.Storages ?? [])
            .Should().Contain(s => s.ExternalId == fullStorage.ExternalId.Value);
    }
}
