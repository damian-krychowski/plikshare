using FluentAssertions;
using Microsoft.AspNetCore.Http;
using PlikShare.AuditLog;
using PlikShare.Core.Utils;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.Apis;
using PlikShare.Storages.Encryption;
using PlikShare.Users.PermissionsAndRoles;
using PlikShare.Users.StorageAccess;
using PlikShare.Users.UpdateMaxWorkspaceNumber.Contracts;
using PlikShare.Workspaces.Create.Contracts;
using Xunit.Abstractions;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.IntegrationTests.TestCases.Users;

[Collection(IntegrationTestsCollection.Name)]
public class user_storage_access_tests : TestFixture, IDisposable
{
    private readonly HostFixture8081 _hostFixture;
    private AppSignedInUser AppOwner { get; }

    public user_storage_access_tests(
        HostFixture8081 hostFixture,
        ITestOutputHelper testOutputHelper) : base(hostFixture, testOutputHelper)
    {
        _hostFixture = hostFixture;
        AppOwner = SignIn(user: Users.AppOwner).Result;
    }

    [Fact]
    public async Task get_user_details_for_existing_user_defaults_to_all_with_no_storages()
    {
        //given
        var invited = await InviteAndRegisterUser(AppOwner);

        //when
        var details = await Api.Users.GetDetails(invited.ExternalId, cookie: AppOwner.Cookie);

        //then
        details.User.StorageAccess.Mode.Should().Be(UserStorageAccessMode.All);
        details.User.StorageAccess.StorageExternalIds.Should().BeEmpty();
    }

    [Fact]
    public async Task invited_user_inherits_general_settings_storage_access_snapshot()
    {
        //given
        var allowedStorage = await CreateHardDriveStorage(AppOwner, StorageEncryptionType.None);
        var blockedStorage = await CreateHardDriveStorage(AppOwner, StorageEncryptionType.None);

        await Api.GeneralSettings.SetNewUserDefaultStorageAccess(
            mode: UserStorageAccessMode.AllowOnly,
            storageExternalIds: [allowedStorage.ExternalId.Value],
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //when
        var invited = await InviteAndRegisterUser(AppOwner);

        //then
        var details = await Api.Users.GetDetails(invited.ExternalId, cookie: AppOwner.Cookie);
        details.User.StorageAccess.Mode.Should().Be(UserStorageAccessMode.AllowOnly);
        details.User.StorageAccess.StorageExternalIds.Should().BeEquivalentTo([allowedStorage.ExternalId.Value]);
        details.User.StorageAccess.StorageExternalIds.Should().NotContain(blockedStorage.ExternalId.Value);
    }

    [Fact]
    public async Task changing_general_settings_after_invitation_does_not_retroactively_update_existing_user()
    {
        //given
        var firstStorage = await CreateHardDriveStorage(AppOwner, StorageEncryptionType.None);
        var secondStorage = await CreateHardDriveStorage(AppOwner, StorageEncryptionType.None);

        await Api.GeneralSettings.SetNewUserDefaultStorageAccess(
            mode: UserStorageAccessMode.AllowOnly,
            storageExternalIds: [firstStorage.ExternalId.Value],
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        var invited = await InviteAndRegisterUser(AppOwner);

        //when — admin changes the default after the user already exists
        await Api.GeneralSettings.SetNewUserDefaultStorageAccess(
            mode: UserStorageAccessMode.AllowOnly,
            storageExternalIds: [secondStorage.ExternalId.Value],
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then — existing user keeps the snapshot taken at invitation time
        var details = await Api.Users.GetDetails(invited.ExternalId, cookie: AppOwner.Cookie);
        details.User.StorageAccess.StorageExternalIds.Should().BeEquivalentTo([firstStorage.ExternalId.Value]);
    }

    [Fact]
    public async Task admin_can_update_user_storage_access()
    {
        //given
        var storage = await CreateHardDriveStorage(AppOwner, StorageEncryptionType.None);
        var invited = await InviteAndRegisterUser(AppOwner);

        //when
        await Api.Users.UpdateStorageAccess(
            userExternalId: invited.ExternalId,
            mode: UserStorageAccessMode.AllowAllExcept,
            storageExternalIds: [storage.ExternalId.Value],
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var details = await Api.Users.GetDetails(invited.ExternalId, cookie: AppOwner.Cookie);
        details.User.StorageAccess.Mode.Should().Be(UserStorageAccessMode.AllowAllExcept);
        details.User.StorageAccess.StorageExternalIds.Should().BeEquivalentTo([storage.ExternalId.Value]);
    }

    [Fact]
    public async Task switching_user_storage_access_to_all_clears_storage_list()
    {
        //given
        var storage = await CreateHardDriveStorage(AppOwner, StorageEncryptionType.None);
        var invited = await InviteAndRegisterUser(AppOwner);

        await Api.Users.UpdateStorageAccess(
            userExternalId: invited.ExternalId,
            mode: UserStorageAccessMode.AllowOnly,
            storageExternalIds: [storage.ExternalId.Value],
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //when
        await Api.Users.UpdateStorageAccess(
            userExternalId: invited.ExternalId,
            mode: UserStorageAccessMode.All,
            storageExternalIds: [storage.ExternalId.Value], // server should ignore for All mode
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var details = await Api.Users.GetDetails(invited.ExternalId, cookie: AppOwner.Cookie);
        details.User.StorageAccess.Mode.Should().Be(UserStorageAccessMode.All);
        details.User.StorageAccess.StorageExternalIds.Should().BeEmpty();
    }

    [Fact]
    public async Task updating_user_storage_access_with_unknown_storage_external_id_returns_400()
    {
        //given
        var invited = await InviteAndRegisterUser(AppOwner);

        //when
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.Users.UpdateStorageAccess(
                userExternalId: invited.ExternalId,
                mode: UserStorageAccessMode.AllowOnly,
                storageExternalIds: ["s_does-not-exist"],
                cookie: AppOwner.Cookie,
                antiforgery: AppOwner.Antiforgery));

        //then
        apiError.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        apiError.HttpError.Should().NotBeNull();
        apiError.HttpError!.Code.Should().Be("unknown-storage-external-ids");
    }

    [Fact]
    public async Task updating_user_storage_access_should_produce_audit_log_entry()
    {
        //given
        var storage = await CreateHardDriveStorage(AppOwner, StorageEncryptionType.None);
        var invited = await InviteAndRegisterUser(AppOwner);

        //when
        await Api.Users.UpdateStorageAccess(
            userExternalId: invited.ExternalId,
            mode: UserStorageAccessMode.AllowOnly,
            storageExternalIds: [storage.ExternalId.Value],
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<Audit.User.StorageAccessUpdated>(
            expectedEventType: AuditLogEventTypes.User.StorageAccessUpdated,
            assertDetails: details =>
            {
                details.Mode.Should().Be(UserStorageAccessMode.AllowOnly);
                details.StorageExternalIds.Should().BeEquivalentTo([storage.ExternalId.Value]);
                details.Target.Email.Should().Be(invited.Email);
            },
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Warning);
    }

    [Fact]
    public async Task dashboard_filters_storages_in_allow_only_mode_for_regular_user()
    {
        //given — two storages, user can see only one
        var allowedStorage = await CreateHardDriveStorage(AppOwner, StorageEncryptionType.None);
        var blockedStorage = await CreateHardDriveStorage(AppOwner, StorageEncryptionType.None);
        var invited = await InviteAndRegisterUser(AppOwner);

        await Api.Users.UpdateStorageAccess(
            userExternalId: invited.ExternalId,
            mode: UserStorageAccessMode.AllowOnly,
            storageExternalIds: [allowedStorage.ExternalId.Value],
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //when
        var dashboard = await Api.Dashboard.Get(cookie: invited.Cookie);

        //then
        dashboard.Storages.Should().Contain(s => s.ExternalId == allowedStorage.ExternalId.Value);
        dashboard.Storages.Should().NotContain(s => s.ExternalId == blockedStorage.ExternalId.Value);
    }

    [Fact]
    public async Task dashboard_filters_storages_in_allow_all_except_mode_for_regular_user()
    {
        //given — two storages, user blocked from one
        var allowedStorage = await CreateHardDriveStorage(AppOwner, StorageEncryptionType.None);
        var blockedStorage = await CreateHardDriveStorage(AppOwner, StorageEncryptionType.None);
        var invited = await InviteAndRegisterUser(AppOwner);

        await Api.Users.UpdateStorageAccess(
            userExternalId: invited.ExternalId,
            mode: UserStorageAccessMode.AllowAllExcept,
            storageExternalIds: [blockedStorage.ExternalId.Value],
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //when
        var dashboard = await Api.Dashboard.Get(cookie: invited.Cookie);

        //then
        dashboard.Storages.Should().Contain(s => s.ExternalId == allowedStorage.ExternalId.Value);
        dashboard.Storages.Should().NotContain(s => s.ExternalId == blockedStorage.ExternalId.Value);
    }

    [Fact]
    public async Task app_owner_bypasses_storage_access_policy_on_dashboard()
    {
        //given — two storages, plus a second app-owner who can write the policy onto AppOwner
        // (the per-user endpoint refuses self-modification via ValidateUserUpdateFilter).
        var storageA = await CreateHardDriveStorage(AppOwner, StorageEncryptionType.None);
        var storageB = await CreateHardDriveStorage(AppOwner, StorageEncryptionType.None);

        var secondOwner = await SignIn(Users.SecondAppOwner);

        await Api.Users.UpdateStorageAccess(
            userExternalId: AppOwner.ExternalId,
            mode: UserStorageAccessMode.AllowOnly,
            storageExternalIds: [storageA.ExternalId.Value],
            cookie: secondOwner.Cookie,
            antiforgery: secondOwner.Antiforgery);

        //when — the AppOwner re-reads the dashboard with the cache refreshed
        var refreshedAppOwner = await SignIn(Users.AppOwner);
        var dashboard = await Api.Dashboard.Get(cookie: refreshedAppOwner.Cookie);

        //then — app owner sees both storages despite the restrictive policy on the row
        dashboard.Storages.Should().Contain(s => s.ExternalId == storageA.ExternalId.Value);
        dashboard.Storages.Should().Contain(s => s.ExternalId == storageB.ExternalId.Value);
    }

    [Fact]
    public async Task creating_workspace_on_disallowed_storage_returns_403_storage_not_allowed_for_user()
    {
        //given
        var allowedStorage = await CreateHardDriveStorage(AppOwner, StorageEncryptionType.None);
        var blockedStorage = await CreateHardDriveStorage(AppOwner, StorageEncryptionType.None);
        var invited = await InviteAndRegisterUser(AppOwner);

        await Api.Users.UpdatePermissionsAndRoles(
            userExternalId: invited.ExternalId,
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
                CanManageAuditLog = false,
                CanManageAgents = false
            },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        await Api.Users.UpdateMaxWorkspaceNumber(
            userExternalId: invited.ExternalId,
            request: new UpdateUserMaxWorkspaceNumberRequestDto { MaxWorkspaceNumber = 1 },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        await Api.Users.UpdateStorageAccess(
            userExternalId: invited.ExternalId,
            mode: UserStorageAccessMode.AllowOnly,
            storageExternalIds: [allowedStorage.ExternalId.Value],
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //when
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.Workspaces.Create(
                request: new CreateWorkspaceRequestDto(
                    StorageExternalId: blockedStorage.ExternalId,
                    Name: $"workspace-{Guid.NewGuid().ToBase62()}"),
                cookie: invited.Cookie,
                antiforgery: invited.Antiforgery));

        //then
        apiError.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        apiError.HttpError.Should().NotBeNull();
        apiError.HttpError!.Code.Should().Be("storage-not-allowed-for-user");
    }

    [Fact]
    public async Task creating_workspace_on_allowed_storage_succeeds_under_restrictive_policy()
    {
        //given — same setup as above but pointing at the allowed storage
        var allowedStorage = await CreateHardDriveStorage(AppOwner, StorageEncryptionType.None);
        var invited = await InviteAndRegisterUser(AppOwner);

        await Api.Users.UpdatePermissionsAndRoles(
            userExternalId: invited.ExternalId,
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
                CanManageAuditLog = false,
                CanManageAgents = false
            },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        await Api.Users.UpdateMaxWorkspaceNumber(
            userExternalId: invited.ExternalId,
            request: new UpdateUserMaxWorkspaceNumberRequestDto { MaxWorkspaceNumber = 1 },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        await Api.Users.UpdateStorageAccess(
            userExternalId: invited.ExternalId,
            mode: UserStorageAccessMode.AllowOnly,
            storageExternalIds: [allowedStorage.ExternalId.Value],
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //when
        var response = await Api.Workspaces.Create(
            request: new CreateWorkspaceRequestDto(
                StorageExternalId: allowedStorage.ExternalId,
                Name: $"workspace-{Guid.NewGuid().ToBase62()}"),
            cookie: invited.Cookie,
            antiforgery: invited.Antiforgery);

        //then
        response.ExternalId.Should().NotBeNull();
    }

    [Fact]
    public async Task storage_names_endpoint_returns_storages_for_admin_with_manage_general_settings()
    {
        //given — admin without manage-storages permission, but with manage-general-settings
        var storage = await CreateHardDriveStorage(AppOwner, StorageEncryptionType.None);
        var admin = await InviteAndRegisterUser(AppOwner);

        await Api.Users.UpdatePermissionsAndRoles(
            userExternalId: admin.ExternalId,
            request: new UserPermissionsAndRolesDto
            {
                IsAdmin = true,
                CanAddWorkspace = false,
                CanManageAuth = false,
                CanManageIntegrations = false,
                CanManageEmailProviders = false,
                CanManageGeneralSettings = true,
                CanManageStorages = false,
                CanManageUsers = false,
                CanManageAuditLog = false,
                CanManageAgents = false
            },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        // Re-sign-in so the cookie carries the admin role + permission claims.
        var adminSignedIn = await SignIn(new User(admin.Email, admin.Password));

        //when
        var response = await Api.Storages.GetNames(cookie: adminSignedIn.Cookie);

        //then
        response.Items.Should().Contain(s => s.ExternalId == storage.ExternalId);
    }

    [Fact]
    public async Task storage_names_endpoint_returns_403_for_regular_user_without_admin_permissions()
    {
        //given
        await CreateHardDriveStorage(AppOwner, StorageEncryptionType.None);
        var regular = await InviteAndRegisterUser(AppOwner);

        //when
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.Storages.GetNames(cookie: regular.Cookie));

        //then
        apiError.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    public void Dispose()
    {
        _hostFixture.ResetGeneralSettings();
    }
}
